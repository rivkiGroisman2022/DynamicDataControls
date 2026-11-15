import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ControlDetails,
  ControlListItem,
  ControlRunResult,
  ControlUpsertRequest,
  DefinitionPreview,
  RunHistoryItem,
} from '../models/api.models';
import { ControlDefinition } from '../models/definition';

/** Access to /api/controls. A thin layer: HTTP only, no logic. */
@Injectable({ providedIn: 'root' })
export class ControlsApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/controls';

  list(): Observable<ControlListItem[]> {
    return this.http.get<ControlListItem[]>(this.base);
  }

  get(id: number): Observable<ControlDetails> {
    return this.http.get<ControlDetails>(`${this.base}/${id}`);
  }

  create(request: ControlUpsertRequest): Observable<ControlDetails> {
    return this.http.post<ControlDetails>(this.base, request);
  }

  update(id: number, request: ControlUpsertRequest): Observable<ControlDetails> {
    return this.http.put<ControlDetails>(`${this.base}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  preview(definition: ControlDefinition): Observable<DefinitionPreview> {
    return this.http.post<DefinitionPreview>(`${this.base}/preview`, definition);
  }

  run(id: number, maxRows = 1000): Observable<ControlRunResult> {
    return this.http.post<ControlRunResult>(`${this.base}/${id}/run`, null, {
      params: new HttpParams().set('maxRows', maxRows),
    });
  }

  testRun(definition: ControlDefinition): Observable<ControlRunResult> {
    return this.http.post<ControlRunResult>(`${this.base}/test-run`, definition);
  }

  history(id: number, take = 10): Observable<RunHistoryItem[]> {
    return this.http.get<RunHistoryItem[]>(`${this.base}/${id}/runs`, { params: new HttpParams().set('take', take) });
  }
}
