import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { EntityInfo, OperatorInfo, SelectableField } from '../models/api.models';

/**
 * Metadata: the entities, fields and operators. Barely changes, so it is cached in memory:
 * shareReplay(1) = the first request goes to the server, and everyone after gets the same answer without another request.
 */
@Injectable({ providedIn: 'root' })
export class MetadataStore {
  private readonly http = inject(HttpClient);
  private readonly fieldsCache = new Map<string, Observable<SelectableField[]>>();

  readonly entities$ = this.http.get<EntityInfo[]>('/api/metadata/entities').pipe(shareReplay(1));
  readonly operators$ = this.http.get<OperatorInfo[]>('/api/metadata/operators').pipe(shareReplay(1));

  fields$(entityKey: string): Observable<SelectableField[]> {
    let cached = this.fieldsCache.get(entityKey);
    if (!cached) {
      cached = this.http
        .get<SelectableField[]>(`/api/metadata/entities/${encodeURIComponent(entityKey)}/fields`)
        .pipe(shareReplay(1));
      this.fieldsCache.set(entityKey, cached);
    }
    return cached;
  }
}
