import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { currentRole } from './admin-session';
import { DashboardDto, DashboardParams } from './admin.models';

const BASE = '/api/bookings/admin';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);

  getDashboard(params: DashboardParams): Observable<DashboardDto> {
    let httpParams = new HttpParams().set('date', params.date);
    if (params.building?.trim()) {
      httpParams = httpParams.set('building', params.building.trim());
    }
    if (params.type) {
      httpParams = httpParams.set('type', params.type);
    }
    return this.http.get<DashboardDto>(`${BASE}/dashboard`, {
      params: httpParams,
      headers: this.headers(),
    });
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ 'X-User-Role': currentRole() ?? '' });
  }
}
