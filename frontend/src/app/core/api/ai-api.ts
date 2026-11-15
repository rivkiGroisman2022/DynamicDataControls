import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Severity, ValidationError } from '../models/api.models';
import { ControlDefinition } from '../models/definition';

/** A control suggestion from the AI - after the server has already validated it against the real metadata. */
export interface AiSuggestion {
  name: string;
  description?: string | null;
  severity: Severity;
  definition: ControlDefinition;
  definitionText?: string | null;
  isValid: boolean;
  errors: ValidationError[];
  explanation: string[];
  confidence: number;
  warnings: string[];
  provider: string;
  model?: string | null;
}

/** Draft passed from the AI screen to the editor (via router state). */
export interface AiDraft {
  name: string;
  description?: string | null;
  severity: Severity;
  definition: ControlDefinition;
}

@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);

  suggestControl(text: string): Observable<AiSuggestion> {
    return this.http.post<AiSuggestion>('/api/ai/suggest-control', { text });
  }

  qualitySuggestions(entity?: string): Observable<AiSuggestion[]> {
    return this.http.post<AiSuggestion[]>('/api/ai/quality-suggestions', { entity: entity ?? null });
  }
}
