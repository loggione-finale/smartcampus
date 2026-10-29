import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { BookingsAvailabilityError, SpacesApiService } from './spaces-api.service';
import {
  EQUIPMENT_OPTIONS,
  SPACE_MODE_HINTS,
  SPACE_MODE_LABELS,
  SPACE_MODE_OPTIONS,
  SPACE_TYPE_LABELS,
  SPACE_TYPE_OPTIONS,
  SpaceDto,
  SpaceMode,
  SpaceSearchParams,
  SpaceType,
  TimeSlotRange,
  extractSpaceFeatures,
  formatEquipmentLabel,
  formatFloorLabel,
  romeLocalToUtcIso,
  todayInRome,
  utcIsoToRomeTime,
} from './spaces.models';

type TimeBandPreset = {
  label: string;
  startTime: string;
  endTime: string;
};

@Component({
  selector: 'app-spaces-catalog-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './spaces-catalog.page.html',
  styleUrl: './spaces-catalog.page.scss',
})
export class SpacesCatalogPage implements OnInit {
  private readonly api = inject(SpacesApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private activeRequest: Subscription | null = null;

  protected readonly typeOptions = SPACE_TYPE_OPTIONS;
  protected readonly modeOptions = SPACE_MODE_OPTIONS;
  protected readonly equipmentOptions = EQUIPMENT_OPTIONS;
  protected readonly typeLabels = SPACE_TYPE_LABELS;
  protected readonly modeLabels = SPACE_MODE_LABELS;
  protected readonly modeHints = SPACE_MODE_HINTS;

  protected readonly timeBandPresets: ReadonlyArray<TimeBandPreset> = [
    { label: 'Mattina (08:00–14:00)', startTime: '08:00', endTime: '14:00' },
    { label: 'Pomeriggio (14:00–19:00)', startTime: '14:00', endTime: '19:00' },
    { label: 'Sera (19:00–23:00)', startTime: '19:00', endTime: '23:00' },
  ];

  protected readonly filterForm = this.formBuilder.group({
    search: this.formBuilder.nonNullable.control('', [Validators.maxLength(100)]),
    type: this.formBuilder.nonNullable.control<SpaceType | ''>(''),
    mode: this.formBuilder.nonNullable.control<SpaceMode | ''>(''),
    minCapacity: this.formBuilder.control<number | null>(null, [
      Validators.min(1),
      Validators.max(500),
    ]),
    building: this.formBuilder.nonNullable.control('', [Validators.maxLength(100)]),
    equipment: this.formBuilder.nonNullable.control<string[]>([]),
    software: this.formBuilder.nonNullable.control('', [Validators.maxLength(100)]),
    date: this.formBuilder.nonNullable.control(''),
    startTime: this.formBuilder.nonNullable.control(''),
    endTime: this.formBuilder.nonNullable.control(''),
  });

  protected readonly spaces = signal<SpaceDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly slotError = signal<string | null>(null);
  protected readonly appliedSlot = signal<TimeSlotRange | null>(null);
  protected readonly hasLoadedOnce = signal(false);

  protected readonly resultsLabel = computed(() => {
    const count = this.spaces().length;
    return count === 1 ? '1 spazio trovato' : `${count} spazi trovati`;
  });

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.activeRequest?.unsubscribe();
    });
  }

  ngOnInit(): void {
    this.loadSpaces({}, null);
  }

  protected applyFilters(): void {
    this.slotError.set(null);
    this.errorMessage.set(null);

    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const raw = this.filterForm.getRawValue();
    const slotResolution = this.resolveSlotRange(raw.date, raw.startTime, raw.endTime);
    if (!slotResolution.valid) {
      this.slotError.set(slotResolution.error);
      return;
    }

    const searchParams: SpaceSearchParams = {
      search: raw.search,
      type: raw.type,
      mode: raw.mode,
      minCapacity: raw.minCapacity,
      building: raw.building,
      equipment: raw.equipment,
      software: raw.software,
    };

    this.loadSpaces(searchParams, slotResolution.slot);
  }

  protected resetFilters(): void {
    this.filterForm.reset({
      search: '',
      type: '',
      mode: '',
      minCapacity: null,
      building: '',
      equipment: [],
      software: '',
      date: '',
      startTime: '',
      endTime: '',
    });
    this.slotError.set(null);
    this.errorMessage.set(null);
    this.loadSpaces({}, null);
  }

  protected selectTimeBandPreset(preset: TimeBandPreset): void {
    const currentDate = this.filterForm.controls.date.value.trim() || todayInRome();
    this.filterForm.patchValue({
      date: currentDate,
      startTime: preset.startTime,
      endTime: preset.endTime,
    });
    this.applyFilters();
  }

  protected clearTimeSlot(): void {
    this.filterForm.patchValue({
      date: '',
      startTime: '',
      endTime: '',
    });
    this.slotError.set(null);
    this.applyFilters();
  }

  protected hasSlotValues(): boolean {
    const { date, startTime, endTime } = this.filterForm.getRawValue();
    return Boolean(date.trim() || startTime.trim() || endTime.trim());
  }

  protected capacityError(): string | null {
    const control = this.filterForm.controls.minCapacity;
    if (!control.touched && !control.dirty) {
      return null;
    }
    if (control.hasError('min')) {
      return 'La capienza minima deve essere almeno 1.';
    }
    if (control.hasError('max')) {
      return 'La capienza massima ricercabile è 500.';
    }
    return null;
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

  protected formatUnavailabilityWindow(startIso: string, endIso: string): string {
    const startTime = utcIsoToRomeTime(startIso);
    const endTime = utcIsoToRomeTime(endIso);
    const day = startIso.slice(0, 10);
    return startTime && endTime ? `${day} · ${startTime}–${endTime}` : day;
  }

  protected spaceAvailabilityHref(spaceId: string): string {
    const chosenDate = this.appliedSlot()?.date || this.filterForm.controls.date.value.trim();
    const encodedId = encodeURIComponent(spaceId);
    return chosenDate
      ? `/spaces/${encodedId}?date=${encodeURIComponent(chosenDate)}`
      : `/spaces/${encodedId}`;
  }

  protected openSpaceAvailability(event: MouseEvent, spaceId: string): void {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    const chosenDate = this.appliedSlot()?.date || this.filterForm.controls.date.value.trim();
    void this.router.navigate(['/spaces', spaceId], {
      queryParams: chosenDate ? { date: chosenDate } : {},
    });
  }

  private resolveSlotRange(
    rawDate: string,
    rawStart: string,
    rawEnd: string,
  ): { valid: true; slot: TimeSlotRange | null } | { valid: false; error: string } {
    const date = rawDate.trim();
    const startTime = rawStart.trim();
    const endTime = rawEnd.trim();

    if (!date && !startTime && !endTime) {
      return { valid: true, slot: null };
    }

    if (!date || !startTime || !endTime) {
      return {
        valid: false,
        error:
          'Per cercare le aule libere in una fascia oraria, compila data, ora di inizio e ora di fine.',
      };
    }

    const startIso = romeLocalToUtcIso(date, startTime);
    const endIso = romeLocalToUtcIso(date, endTime);

    if (!startIso || !endIso) {
      return {
        valid: false,
        error: 'Inserisci una data e un intervallo orario validi.',
      };
    }

    if (Date.parse(endIso) <= Date.parse(startIso)) {
      return {
        valid: false,
        error: 'L’ora di fine deve essere successiva all’ora di inizio.',
      };
    }

    return {
      valid: true,
      slot: {
        date,
        startTime,
        endTime,
        startIso,
        endIso,
      },
    };
  }

  private loadSpaces(params: SpaceSearchParams, slotRange: TimeSlotRange | null): void {
    this.activeRequest?.unsubscribe();
    this.loading.set(true);
    this.errorMessage.set(null);

    this.activeRequest = this.api.searchCatalog(params, slotRange).subscribe({
      next: (items) => {
        this.spaces.set(items);
        this.appliedSlot.set(slotRange);
        this.loading.set(false);
        this.hasLoadedOnce.set(true);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.hasLoadedOnce.set(true);
        this.errorMessage.set(this.mapErrorToMessage(err));
      },
    });
  }

  private mapErrorToMessage(error: unknown): string {
    if (error instanceof BookingsAvailabilityError) {
      return 'Impossibile verificare quali spazi siano liberi nella fascia selezionata: il servizio prenotazioni non risponde. Riprova tra poco o rimuovi il filtro orario.';
    }

    if (error instanceof HttpErrorResponse) {
      if (error.status === 400) {
        const detail = (error.error as { detail?: unknown } | null)?.detail;
        return typeof detail === 'string' && detail.trim()
          ? `Parametri di ricerca non validi: ${detail}`
          : 'Controlla i parametri di ricerca inseriti.';
      }

      if (error.status === 503 || error.status === 0) {
        return 'Il catalogo degli spazi non è al momento raggiungibile. Riprova tra qualche istante.';
      }
    }

    return 'Si è verificato un errore durante il caricamento degli spazi. Riprova tra poco.';
  }
}
