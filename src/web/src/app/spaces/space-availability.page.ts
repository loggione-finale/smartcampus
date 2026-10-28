import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, combineLatest, forkJoin } from 'rxjs';
import { SpacesApiService } from './spaces-api.service';
import {
  DailyTimelineSegment,
  SPACE_MODE_HINTS,
  SPACE_MODE_LABELS,
  SPACE_TYPE_LABELS,
  SpaceAvailabilityDto,
  SpaceDto,
  TimelineSegmentStatus,
  buildDailyTimeline,
  extractSpaceFeatures,
  formatEquipmentLabel,
  formatFloorLabel,
  shiftIsoDate,
  todayInRome,
} from './spaces.models';

export const BOOKINGS_CHANGED_EVENT = 'smartcampus:bookings-changed';
const BOOKINGS_BROADCAST_CHANNEL = 'smartcampus-bookings';
const VALID_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

@Component({
  selector: 'app-space-availability-page',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  templateUrl: './space-availability.page.html',
  styleUrl: './space-availability.page.scss',
})
export class SpaceAvailabilityPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(SpacesApiService);
  private readonly destroyRef = inject(DestroyRef);

  private activeRequest: Subscription | null = null;
  private routeSubscription: Subscription | null = null;
  private broadcastChannel: BroadcastChannel | null = null;

  protected readonly typeLabels = SPACE_TYPE_LABELS;
  protected readonly modeLabels = SPACE_MODE_LABELS;
  protected readonly modeHints = SPACE_MODE_HINTS;

  protected readonly spaceId = signal('');
  protected readonly selectedDate = signal(todayInRome());
  protected readonly dateControl = new FormControl(this.selectedDate(), { nonNullable: true });

  protected readonly space = signal<SpaceDto | null>(null);
  protected readonly availability = signal<SpaceAvailabilityDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly notFound = signal(false);
  protected readonly lastUpdatedLabel = signal<string | null>(null);

  protected readonly timeline = computed<DailyTimelineSegment[]>(() => {
    const currentSpace = this.space();
    const currentAvailability = this.availability();
    const date = this.selectedDate();
    if (!currentSpace || !currentAvailability) {
      return [];
    }
    return buildDailyTimeline(currentSpace, currentAvailability, date);
  });

  protected readonly bookableSegmentsCount = computed(
    () =>
      this.timeline().filter((seg) => seg.status === 'free' || seg.status === 'partial').length,
  );

  protected readonly busySegmentsCount = computed(
    () =>
      this.timeline().filter(
        (seg) =>
          seg.status === 'occupied' || seg.status === 'unavailable' || seg.status === 'inactive',
      ).length,
  );

  constructor() {
    const onBookingsChanged = () => {
      if (this.spaceId()) {
        this.reloadAvailability();
      }
    };

    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible' && this.spaceId() && !this.loading()) {
        this.reloadAvailability();
      }
    };

    window.addEventListener(BOOKINGS_CHANGED_EVENT, onBookingsChanged);
    document.addEventListener('visibilitychange', onVisibilityChange);

    if (typeof BroadcastChannel !== 'undefined') {
      try {
        this.broadcastChannel = new BroadcastChannel(BOOKINGS_BROADCAST_CHANNEL);
        this.broadcastChannel.onmessage = () => onBookingsChanged();
      } catch {
        this.broadcastChannel = null;
      }
    }

    this.destroyRef.onDestroy(() => {
      window.removeEventListener(BOOKINGS_CHANGED_EVENT, onBookingsChanged);
      document.removeEventListener('visibilitychange', onVisibilityChange);
      this.broadcastChannel?.close();
      this.activeRequest?.unsubscribe();
      this.routeSubscription?.unsubscribe();
    });
  }

  ngOnInit(): void {
    this.routeSubscription = combineLatest([
      this.route.paramMap,
      this.route.queryParamMap,
    ]).subscribe(([params, queryParams]) => {
      const id = (params.get('id') ?? '').trim();
      const rawDate = (queryParams.get('date') ?? '').trim();
      const resolvedDate = VALID_DATE_PATTERN.test(rawDate) ? rawDate : todayInRome();

      this.spaceId.set(id);
      this.selectedDate.set(resolvedDate);
      if (this.dateControl.value !== resolvedDate) {
        this.dateControl.setValue(resolvedDate, { emitEvent: false });
      }

      if (!id) {
        this.notFound.set(true);
        this.errorMessage.set('Identificativo dello spazio non valido.');
        return;
      }

      this.reloadAvailability();
    });
  }

  protected onDateInputChange(): void {
    const nextDate = this.dateControl.value.trim();
    if (!VALID_DATE_PATTERN.test(nextDate)) {
      return;
    }
    this.navigateToDate(nextDate);
  }

  protected shiftDay(deltaDays: number): void {
    const nextDate = shiftIsoDate(this.selectedDate(), deltaDays);
    this.navigateToDate(nextDate);
  }

  protected goToToday(): void {
    this.navigateToDate(todayInRome());
  }

  protected reloadAvailability(): void {
    const id = this.spaceId();
    const date = this.selectedDate();
    if (!id || !VALID_DATE_PATTERN.test(date)) {
      return;
    }

    this.activeRequest?.unsubscribe();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.notFound.set(false);

    this.activeRequest = forkJoin({
      space: this.api.getSpaceById(id),
      availability: this.api.getSpaceDailyAvailability(id, date),
    }).subscribe({
      next: ({ space, availability }) => {
        this.space.set(space);
        this.availability.set(availability);
        this.loading.set(false);
        this.lastUpdatedLabel.set(
          new Intl.DateTimeFormat('it-IT', {
            timeZone: 'Europe/Rome',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hourCycle: 'h23',
          }).format(new Date()),
        );
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.notFound.set(true);
          this.errorMessage.set('Lo spazio richiesto non esiste o non è più presente a catalogo.');
          return;
        }
        this.errorMessage.set(this.mapError(err));
      },
    });
  }

  protected backToCatalog(event: MouseEvent): void {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    void this.router.navigate(['/spaces']);
  }

  protected statusBadgeLabel(status: TimelineSegmentStatus, isMixed: boolean): string {
    switch (status) {
      case 'free':
        return 'Libera';
      case 'partial':
        return 'Posti disponibili';
      case 'occupied':
        return isMixed ? 'Completa' : 'Occupata';
      case 'unavailable':
        return 'Indisponibile';
      case 'inactive':
        return 'Disattivato';
    }
  }

  protected segmentSummary(segment: DailyTimelineSegment, isMixed: boolean): string {
    if (segment.status === 'inactive') {
      return segment.reason ?? 'Spazio disattivato.';
    }
    if (segment.status === 'unavailable') {
      return segment.reason
        ? `Chiusura / manutenzione: ${segment.reason}`
        : 'Spazio non prenotabile in questa fascia.';
    }
    if (isMixed) {
      if (segment.status === 'free') {
        return `Tutti i ${segment.capacity} posti sono liberi`;
      }
      if (segment.status === 'partial') {
        return `${segment.freeSeats} posti liberi su ${segment.capacity} (${segment.occupiedSeats} occupati)`;
      }
      return `0 posti liberi su ${segment.capacity} (capienza esaurita)`;
    }

    return segment.status === 'free'
      ? 'Aula interamente libera e prenotabile'
      : 'Aula già prenotata per l’intera fascia';
  }

  protected occupancyPercent(segment: DailyTimelineSegment): number {
    if (segment.capacity <= 0) {
      return 0;
    }
    return Math.min(100, Math.round((segment.occupiedSeats / segment.capacity) * 100));
  }

  protected equipmentLabel(slug: string): string {
    return formatEquipmentLabel(slug);
  }

  protected floorLabel(floor: number): string {
    return formatFloorLabel(floor);
  }

  protected spaceFeatures(space: SpaceDto): string[] {
    return extractSpaceFeatures(space.features);
  }

  private navigateToDate(nextDate: string): void {
    if (nextDate === this.selectedDate()) {
      this.reloadAvailability();
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { date: nextDate },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private mapError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 400) {
        return 'La data selezionata non è valida. Scegli una data nel formato AAAA-MM-GG.';
      }
      if (error.status === 503 || error.status === 0) {
        return 'Impossibile consultare la disponibilità in questo momento: uno dei servizi non risponde. Riprova tra poco.';
      }
    }
    return 'Si è verificato un errore durante il caricamento della disponibilità.';
  }
}
