import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, filter, map, Observable, of, switchMap } from 'rxjs';
import { AiDraft } from '../../../core/api/ai-api';
import { toApiError } from '../../../core/api/api-error';
import { ControlsApi } from '../../../core/api/controls-api';
import { MetadataStore } from '../../../core/api/metadata-store';
import {
  ControlDetails, ControlRunResult, ControlUpsertRequest, DefinitionPreview, Severity, ValidationError,
} from '../../../core/models/api.models';
import { ConditionGroup, ControlDefinition } from '../../../core/models/definition';
import { asRootGroup, cleanDefinition, countRules, emptyGroup } from '../../../core/models/definition-tree';
import { Notifier } from '../../../core/notifier';
import { ConfirmDialog, ConfirmDialogData } from '../../../shared/confirm-dialog';
import { RunResults } from '../../results/run-results/run-results';
import { ConditionGroupEditor } from '../condition-builder/condition-group';

/**
 * Create / edit a control.
 * State management: every part of the form is a signal; the definition itself is a computed derived from them;
 * the preview (description in words + validity) is computed on the server, debounced 400ms after every change.
 */
@Component({
  selector: 'app-control-editor',
  imports: [
    RouterLink, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule, MatSlideToggleModule,
    MatExpansionModule, MatProgressBarModule, MatProgressSpinnerModule, ConditionGroupEditor, RunResults,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './control-editor.html',
  styleUrl: './control-editor.scss',
})
export class ControlEditor implements OnInit {
  private readonly api = inject(ControlsApi);
  private readonly metadata = inject(MetadataStore);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  /** From the route (withComponentInputBinding): /controls/:id/edit or /controls/new?copyFrom=5 */
  readonly id = input<string>();
  readonly copyFrom = input<string>();

  // ----- Form state -----
  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly severity = signal<Severity>('medium');
  protected readonly isActive = signal(true);
  protected readonly source = signal<'manual' | 'ai'>('manual');
  protected readonly entityKey = signal('');
  protected readonly root = signal<ConditionGroup>(emptyGroup());

  // ----- Screen state -----
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly testing = signal(false);
  protected readonly submitted = signal(false);
  protected readonly serverErrors = signal<ValidationError[]>([]);
  protected readonly testResult = signal<ControlRunResult | null>(null);

  protected readonly isEdit = computed(() => !!this.id());

  // ----- Metadata (from the cache) -----
  protected readonly entities = toSignal(this.metadata.entities$.pipe(map((list) => list.filter((e) => e.isControlTarget))), {
    initialValue: [],
  });
  protected readonly operators = toSignal(this.metadata.operators$, { initialValue: [] });
  /** The available fields change automatically when the entity changes (switchMap cancels a stale request). */
  protected readonly fields = toSignal(
    toObservable(this.entityKey).pipe(switchMap((key) => (key ? this.metadata.fields$(key) : of([])))),
    { initialValue: [] },
  );
  protected readonly entity = computed(() => this.entities().find((e) => e.key === this.entityKey()));

  /** The control definition - derived from the form state. Never stored separately, so it can never be "out of sync". */
  protected readonly definition = computed<ControlDefinition>(() =>
    cleanDefinition({ version: 1, entity: this.entityKey(), condition: this.root() }),
  );
  protected readonly ruleCount = computed(() => countRules(this.root()));

  /** Live preview from the server. The server is the single source of truth for validity - no duplicated validation logic. */
  protected readonly preview = toSignal(
    toObservable(this.definition).pipe(
      debounceTime(400),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      filter((d) => !!d.entity),
      switchMap((d): Observable<DefinitionPreview | null> => this.api.preview(d).pipe(catchError(() => of(null)))),
    ),
    { initialValue: null },
  );

  /** Errors by path. Shown only after a save/test attempt, so the user is not "yelled at" while still typing. */
  protected readonly errorMap = computed<ReadonlyMap<string, string[]>>(() => {
    const map = new Map<string, string[]>();
    if (!this.submitted()) return map;
    const list = this.preview()?.errors ?? this.serverErrors().filter((e) => e.path.startsWith('definition'));
    for (const e of list) {
      const key = e.path.replace(/^definition\.?/, '');
      map.set(key, [...(map.get(key) ?? []), e.message]);
    }
    return map;
  });

  protected readonly nameError = computed(() => {
    if (!this.submitted()) return null;
    if (!this.name().trim()) return 'יש להזין שם לבקרה';
    return this.serverErrors().find((e) => e.path === 'name')?.message ?? null;
  });

  ngOnInit(): void {
    // A draft from the AI screen ("edit in the editor") arrives through the navigation state.
    const draft = (history.state as { aiDraft?: AiDraft } | null)?.aiDraft;
    const sourceId = this.id() ?? this.copyFrom();
    if (draft && !this.id()) {
      this.name.set(draft.name);
      this.description.set(draft.description ?? '');
      this.severity.set(draft.severity);
      this.source.set('ai');
      this.entityKey.set(draft.definition.entity);
      this.root.set(asRootGroup(draft.definition.condition));
    } else if (sourceId) {
      this.loadFrom(Number(sourceId), !this.id());
    } else {
      // New control: default to the first entity
      this.metadata.entities$.subscribe((list) => {
        if (!this.entityKey()) this.entityKey.set(list.find((e) => e.isControlTarget)?.key ?? '');
      });
    }
  }

  private loadFrom(id: number, asCopy: boolean): void {
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (control) => {
        this.fill(control, asCopy);
        this.loading.set(false);
      },
      error: (e) => {
        this.loading.set(false);
        this.notifier.error(e);
        this.router.navigate(['/controls']);
      },
    });
  }

  private fill(control: ControlDetails, asCopy: boolean): void {
    this.name.set(asCopy ? `${control.name} (עותק)` : control.name);
    this.description.set(control.description ?? '');
    this.severity.set(control.severity);
    this.isActive.set(control.isActive);
    this.source.set(asCopy ? 'manual' : control.source);
    this.entityKey.set(control.definition.entity);
    this.root.set(asRootGroup(control.definition.condition));
  }

  protected changeEntity(key: string): void {
    const hasWork = this.ruleCount() > 1 || this.root().conditions.some((c) => c.type === 'rule' && c.field);
    if (!hasWork) {
      this.applyEntity(key);
      return;
    }
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: { title: 'החלפת הנתונים', message: 'החלפת סוג הנתונים תמחק את התנאים שהוגדרו. להמשיך?', confirmText: 'החלפה' },
        direction: 'rtl',
      })
      .afterClosed()
      .subscribe((ok) => (ok ? this.applyEntity(key) : this.entityKey.set(this.entityKey())));
  }

  private applyEntity(key: string): void {
    this.entityKey.set(key);
    this.root.set(emptyGroup());
    this.testResult.set(null);
    this.submitted.set(false);
  }

  protected testRun(): void {
    this.submitted.set(true);
    if (this.preview()?.isValid === false) {
      this.notifier.error(new Error('יש להשלים את התנאים המסומנים לפני הרצה'));
      return;
    }
    this.testing.set(true);
    this.api.testRun(this.definition()).subscribe({
      next: (result) => {
        this.testResult.set(result);
        this.testing.set(false);
      },
      error: (e) => {
        this.testing.set(false);
        this.handleError(e);
      },
    });
  }

  protected save(runAfter = false): void {
    this.submitted.set(true);
    if (!this.name().trim()) return;

    const request: ControlUpsertRequest = {
      name: this.name().trim(),
      description: this.description().trim() || null,
      severity: this.severity(),
      isActive: this.isActive(),
      source: this.source(),
      definition: this.definition(),
    };

    this.saving.set(true);
    const call = this.isEdit() ? this.api.update(Number(this.id()), request) : this.api.create(request);
    call.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.notifier.success(this.isEdit() ? 'הבקרה עודכנה' : 'הבקרה נשמרה');
        this.router.navigate(runAfter ? ['/controls', saved.id, 'results'] : ['/controls']);
      },
      error: (e) => {
        this.saving.set(false);
        this.handleError(e);
      },
    });
  }

  private handleError(error: unknown): void {
    const apiError = toApiError(error);
    if (apiError.status === 409) {
      this.serverErrors.set([{ path: 'name', message: apiError.detail }]);
    } else {
      this.serverErrors.set(apiError.errors);
    }
    this.notifier.error(apiError.errors.length ? new Error('יש לתקן את השגיאות המסומנות') : apiError);
  }
}
