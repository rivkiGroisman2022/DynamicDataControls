import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { map } from 'rxjs';
import { AiApi, AiDraft, AiSuggestion } from '../../core/api/ai-api';
import { toApiError } from '../../core/api/api-error';
import { ControlsApi } from '../../core/api/controls-api';
import { MetadataStore } from '../../core/api/metadata-store';
import { ControlRunResult, Severity } from '../../core/models/api.models';
import { Notifier } from '../../core/notifier';
import { SeverityBadge } from '../../shared/severity-badge';
import { RunResults } from '../results/run-results/run-results';

/** UI state of one suggestion: the suggestion itself + what the user changed + a test run. */
export interface SuggestionState {
  id: number;
  suggestion: AiSuggestion;
  name: string;
  severity: Severity;
  testResult: ControlRunResult | null;
  busy: boolean;
}

/**
 * Stage B - creating a control from a free-text description.
 * Principle: the AI suggests → the user sees what it understood (in words, with an explanation and confidence) → approves.
 * Nothing is saved or run without an explicit user action.
 */
@Component({
  selector: 'app-ai-assistant',
  imports: [
    DecimalPipe, NgTemplateOutlet, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatChipsModule, MatSelectModule,
    MatProgressBarModule, MatProgressSpinnerModule, MatTooltipModule, RunResults, SeverityBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ai-assistant.html',
  styleUrl: './ai-assistant.scss',
})
export class AiAssistant {
  private readonly ai = inject(AiApi);
  private readonly controls = inject(ControlsApi);
  private readonly metadata = inject(MetadataStore);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private nextId = 1;

  protected readonly examples = [
    'מצא עובדים פעילים שגילם נמוך מ־18 ומשכורתם גבוהה מ־15,000 ש"ח',
    'עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם',
    'עובדים פעילים המשויכים למחלקה שאינה פעילה',
    'עובדים פעילים ללא כתובת אימייל או ללא תעודת זהות',
    'מחלקות פעילות בסניף סגור',
    'פרויקטים עם תקציב שלילי',
  ];

  protected readonly text = signal('');
  protected readonly thinking = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly current = signal<SuggestionState | null>(null);

  protected readonly entities = toSignal(this.metadata.entities$.pipe(map((e) => e.filter((x) => x.isControlTarget))), {
    initialValue: [],
  });
  protected readonly qualityEntity = signal<string>('');
  protected readonly qualityLoading = signal(false);
  protected readonly quality = signal<SuggestionState[]>([]);

  protected useExample(example: string): void {
    this.text.set(example);
    this.analyze();
  }

  protected analyze(): void {
    const text = this.text().trim();
    if (!text) return;
    this.thinking.set(true);
    this.error.set(null);
    this.current.set(null);
    this.ai.suggestControl(text).subscribe({
      next: (s) => {
        this.current.set(this.toState(s));
        this.thinking.set(false);
      },
      error: (e) => {
        this.error.set(toApiError(e).userMessage);
        this.thinking.set(false);
      },
    });
  }

  protected loadQuality(): void {
    this.qualityLoading.set(true);
    this.ai.qualitySuggestions(this.qualityEntity() || undefined).subscribe({
      next: (list) => {
        this.quality.set(list.map((s) => this.toState(s)));
        this.qualityLoading.set(false);
      },
      error: (e) => {
        this.qualityLoading.set(false);
        this.notifier.error(e);
      },
    });
  }

  // ----- Actions on a suggestion (the same ones for the main suggestion and the proactive ones) -----

  protected testRun(state: SuggestionState): void {
    this.patch(state.id, { busy: true });
    this.controls.testRun(state.suggestion.definition).subscribe({
      next: (result) => this.patch(state.id, { testResult: result, busy: false }),
      error: (e) => {
        this.patch(state.id, { busy: false });
        this.notifier.error(e);
      },
    });
  }

  protected approveAndSave(state: SuggestionState): void {
    if (!state.name.trim()) {
      this.notifier.error(new Error('יש להזין שם לבקרה'));
      return;
    }
    this.patch(state.id, { busy: true });
    this.controls
      .create({
        name: state.name.trim(),
        description: state.suggestion.description,
        severity: state.severity,
        isActive: true,
        source: 'ai',
        definition: state.suggestion.definition,
      })
      .subscribe({
        next: (saved) => {
          this.notifier.success('הבקרה אושרה ונשמרה');
          this.router.navigate(['/controls', saved.id, 'results']);
        },
        error: (e) => {
          this.patch(state.id, { busy: false });
          this.notifier.error(e);
        },
      });
  }

  /** Continue refining in the regular editor - the AI suggestion becomes the starting point. */
  protected openInEditor(state: SuggestionState): void {
    const draft: AiDraft = {
      name: state.name,
      description: state.suggestion.description,
      severity: state.severity,
      definition: state.suggestion.definition,
    };
    this.router.navigate(['/controls/new'], { state: { aiDraft: draft } });
  }

  protected setName(state: SuggestionState, name: string): void {
    this.patch(state.id, { name });
  }

  protected setSeverity(state: SuggestionState, severity: Severity): void {
    this.patch(state.id, { severity });
  }

  private toState(suggestion: AiSuggestion): SuggestionState {
    return { id: this.nextId++, suggestion, name: suggestion.name, severity: suggestion.severity, testResult: null, busy: false };
  }

  /** Immutable update by id - works the same for the main suggestion and for a suggestion in the proactive list. */
  private patch(id: number, changes: Partial<SuggestionState>): void {
    this.current.update((s) => (s?.id === id ? { ...s, ...changes } : s));
    this.quality.update((list) => list.map((s) => (s.id === id ? { ...s, ...changes } : s)));
  }
}
