import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, throwError } from 'rxjs';
import {
  BatchAvailabilityPayload,
  EquipmentCatalogItemDto,
  SpaceAvailabilityDto,
  SpaceDto,
  SpaceSearchParams,
  TimeSlotRange,
  hasOverlappingUnavailability,
} from './spaces.models';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_BATCH_AVAILABILITY_IDS = 200;

export class BookingsAvailabilityError extends Error {
  constructor(public readonly causeError: HttpErrorResponse) {
    super('Unable to verify availability on the Bookings service.');
    this.name = 'BookingsAvailabilityError';
  }
}

@Injectable({
  providedIn: 'root',
})
export class SpacesApiService {
  private readonly http = inject(HttpClient);
  private bookingsAvailabilityBase:
    | '/api/bookings/availability'
    | '/api/bookings/bookings/availability' = '/api/bookings/availability';

  searchSpaces(params: SpaceSearchParams = {}): Observable<SpaceDto[]> {
    const httpParams = this.buildHttpParams(params);
    return this.http.get<SpaceDto[]>('/api/spaces/spaces', { params: httpParams });
  }

  getSpaceById(id: string): Observable<SpaceDto> {
    const encodedId = encodeURIComponent(id.trim());
    return this.http.get<SpaceDto>(`/api/spaces/spaces/${encodedId}`);
  }

  getEquipmentCatalog(): Observable<EquipmentCatalogItemDto[]> {
    return this.http.get<EquipmentCatalogItemDto[]>('/api/spaces/equipment');
  }

  getSpaceDailyAvailability(spaceId: string, date: string): Observable<SpaceAvailabilityDto> {
    const encodedId = encodeURIComponent(spaceId.trim());
    const httpParams = new HttpParams().set('date', date.trim());
    const primaryBase = this.bookingsAvailabilityBase;

    return this.http.get<SpaceAvailabilityDto>(`${primaryBase}/${encodedId}`, { params: httpParams }).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 404 && !this.hasProblemTitle(error)) {
          const alternateBase = this.alternateBookingsBase(primaryBase);
          this.bookingsAvailabilityBase = alternateBase;
          return this.http.get<SpaceAvailabilityDto>(`${alternateBase}/${encodedId}`, {
            params: httpParams,
          });
        }
        return throwError(() => error);
      }),
    );
  }

  getAvailableSpaceIds(spaceIds: string[], startIso: string, endIso: string): Observable<string[]> {
    const validIds = Array.from(
      new Set(spaceIds.map((id) => id.trim()).filter((id) => UUID_PATTERN.test(id))),
    ).slice(0, MAX_BATCH_AVAILABILITY_IDS);

    if (validIds.length === 0) {
      return of([]);
    }

    const payload: BatchAvailabilityPayload = {
      spaceIds: validIds,
      start: startIso,
      end: endIso,
    };

    const primaryBase = this.bookingsAvailabilityBase;

    return this.http.post<string[]>(primaryBase, payload).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 404 && !this.hasProblemTitle(error)) {
          const alternateBase = this.alternateBookingsBase(primaryBase);
          this.bookingsAvailabilityBase = alternateBase;
          return this.http.post<string[]>(alternateBase, payload);
        }
        return throwError(() => error);
      }),
    );
  }

  searchCatalog(
    params: SpaceSearchParams = {},
    slotRange: TimeSlotRange | null = null,
  ): Observable<SpaceDto[]> {
    return this.searchSpaces(params).pipe(
      switchMap((spaces) => {
        const activeSpaces = spaces.filter((space) => space.isActive);
        if (!slotRange) {
          return of(activeSpaces);
        }

        const withoutMaintenance = activeSpaces.filter(
          (space) => !hasOverlappingUnavailability(space, slotRange.startIso, slotRange.endIso),
        );

        if (withoutMaintenance.length === 0) {
          return of([]);
        }

        const candidateIds = withoutMaintenance.map((space) => space.id);

        return this.getAvailableSpaceIds(candidateIds, slotRange.startIso, slotRange.endIso).pipe(
          map((availableIds) => {
            const availableSet = new Set(availableIds.map((id) => id.toLowerCase()));
            return withoutMaintenance.filter((space) => availableSet.has(space.id.toLowerCase()));
          }),
          catchError((error: HttpErrorResponse) =>
            throwError(() => new BookingsAvailabilityError(error)),
          ),
        );
      }),
    );
  }

  private alternateBookingsBase(
    current: '/api/bookings/bookings/availability' | '/api/bookings/availability',
  ): '/api/bookings/bookings/availability' | '/api/bookings/availability' {
    return current === '/api/bookings/bookings/availability'
      ? '/api/bookings/availability'
      : '/api/bookings/bookings/availability';
  }

  private buildHttpParams(params: SpaceSearchParams): HttpParams {
    let httpParams = new HttpParams();

    if (params.type) {
      httpParams = httpParams.set('type', params.type);
    }

    if (params.mode) {
      httpParams = httpParams.set('mode', params.mode);
    }

    if (typeof params.minCapacity === 'number' && !Number.isNaN(params.minCapacity)) {
      httpParams = httpParams.set('minCapacity', String(params.minCapacity));
    }

    if (params.building?.trim()) {
      httpParams = httpParams.set('building', params.building.trim());
    }

    if (params.search?.trim()) {
      httpParams = httpParams.set('search', params.search.trim());
    }

    if (params.software?.trim()) {
      httpParams = httpParams.set('software', params.software.trim());
    }

    for (const item of params.equipment ?? []) {
      const normalized = item.trim();
      if (normalized) {
        httpParams = httpParams.append('equipment', normalized);
      }
    }

    return httpParams;
  }

  private hasProblemTitle(error: HttpErrorResponse): boolean {
    if (!error.error || typeof error.error !== 'object') {
      return false;
    }
    const payload = error.error as { title?: unknown };
    return typeof payload.title === 'string' && payload.title.trim().length > 0;
  }
}
