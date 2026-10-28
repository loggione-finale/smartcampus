import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { currentToken } from './admin-session';
import { DashboardParams, DashboardSpace, StatsGroup, StatsParams } from './admin.models';

const BASE = '/api/bookings/admin';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);

  getDashboard(params: DashboardParams): Observable<DashboardSpace[]> {
    let httpParams = new HttpParams().set('date', params.date);
    if (params.building?.trim()) {
      httpParams = httpParams.set('building', params.building.trim());
    }
    if (params.type) {
      httpParams = httpParams.set('type', params.type);
    }
    return this.http.get<DashboardSpace[]>(`${BASE}/dashboard`, {
      params: httpParams,
      headers: this.headers(),
    });
  }

  getStats(params: StatsParams): Observable<StatsGroup[]> {
    const httpParams = new HttpParams()
      .set('from', params.from)
      .set('to', params.to)
      .set('groupBy', params.groupBy);
    return this.http.get<StatsGroup[]>(`${BASE}/stats`, {
      params: httpParams,
      headers: this.headers(),
    });
  }

  private headers(): HttpHeaders {
    const token = currentToken();
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }
}
