import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SpaceDto, SpaceMode, SpaceType } from '../spaces/spaces.models';

export interface SpaceInput {
  name: string;
  type: SpaceType;
  mode: SpaceMode;
  building: string;
  floor: number;
  capacity: number;
  equipment: string[];
  features: Record<string, unknown>;
}

export interface EquipmentItem {
  code: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class SpacesAdminApiService {
  private readonly http = inject(HttpClient);

  listAll(): Observable<SpaceDto[]> {
    const params = new HttpParams().set('includeInactive', true);
    return this.http.get<SpaceDto[]>('/api/spaces/spaces', { params });
  }

  getById(id: string): Observable<SpaceDto> {
    return this.http.get<SpaceDto>(`/api/spaces/spaces/${encodeURIComponent(id)}`);
  }

  getEquipmentCatalog(): Observable<EquipmentItem[]> {
    return this.http.get<EquipmentItem[]>('/api/spaces/spaces/equipment');
  }

  create(input: SpaceInput): Observable<SpaceDto> {
    return this.http.post<SpaceDto>('/api/spaces/spaces', input);
  }

  update(id: string, input: SpaceInput): Observable<SpaceDto> {
    return this.http.put<SpaceDto>(`/api/spaces/spaces/${encodeURIComponent(id)}`, input);
  }

  setActive(id: string, active: boolean): Observable<SpaceDto> {
    const action = active ? 'activate' : 'deactivate';
    return this.http.post<SpaceDto>(`/api/spaces/spaces/${encodeURIComponent(id)}/${action}`, {});
  }
}
