import { Component, computed, inject, signal } from '@angular/core';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { SpacesApiService } from '../spaces/spaces-api.service';
import {
  EquipmentCatalogItemDto,
  SpaceAvailabilityDto,
  SpaceDto,
  SpaceMode,
} from '../spaces/spaces.models';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { merge } from 'rxjs';
import { AuthSessionService } from '../auth/auth-session.service';

type BookingSpace = {
  id: string;
  name: string;
  detail: string;
  equipment: string[];
  capacity: number;
  mode: SpaceMode;
};

type EquipmentOption = {
  code: string;
  name: string;
  available: boolean;
};

type BookingField = 'spaceId' | 'date' | 'start' | 'end';

// Maximum total people (organizer included) allowed in a mixed space; enforced by Bookings (#211).
export const MAX_MIXED_GROUP_SIZE = 4;

type BookingResponse = {
  referenceCode?: string;
};

const BOOKING_TOKEN_STORAGE_KEY = 'smartcampus.auth.token';

// The booking service knows these codes; the labels come from the Spaces catalog.
const FALLBACK_EQUIPMENT: EquipmentCatalogItemDto[] = [
  { code: 'videoprojector', name: 'Videoproiettore', description: '', isStandard: true, requiresNotes: false },
  { code: 'interactive-whiteboard', name: 'Lavagna interattiva', description: '', isStandard: true, requiresNotes: false },
  { code: 'audio-kit', name: 'Kit audio', description: '', isStandard: true, requiresNotes: false },
  { code: 'microphone', name: 'Microfono', description: '', isStandard: true, requiresNotes: false },
];

// Decision 32: a booking can start at most 15 days from now, like the Bookings validator.
const MAX_ADVANCE_DAYS = 15;

function localDate(date: Date): string {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
}

function addDays(isoDate: string, days: number): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  return localDate(new Date(year, month - 1, day + days));
}

function hhmm(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
}

// The Bookings service validates time bands in the campus time zone (Europe/Rome).
function campusTimeParts(date: Date): { date: string; minutes: number } {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Rome',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);
  const value = (type: string) => parts.find((part) => part.type === type)?.value ?? '';
  return {
    date: `${value('year')}-${value('month')}-${value('day')}`,
    minutes: Number(value('hour')) * 60 + Number(value('minute')),
  };
}

const BOOKING_BANDS: ReadonlyArray<readonly [number, number]> = [
  [8 * 60, 14 * 60],
  [14 * 60, 19 * 60],
  [19 * 60, 23 * 60],
];

// First full hour that starts in the future inside a bookable band, whatever the device time zone.
function defaultBookingSlot(now: Date = new Date()): { date: string; start: string; end: string } {
  const step = 15 * 60000;
  const firstSlot = Math.ceil((now.getTime() + 60000) / step) * step;

  for (let index = 0; index < 8 * 24 * 4; index += 1) {
    const slotStart = new Date(firstSlot + index * step);
    const slotEnd = new Date(slotStart.getTime() + 60 * 60000);
    const startParts = campusTimeParts(slotStart);
    const endParts = campusTimeParts(slotEnd);
    const band = BOOKING_BANDS.find(
      ([from, to]) => startParts.minutes >= from && startParts.minutes < to,
    );
    const fits =
      band !== undefined &&
      endParts.date === startParts.date &&
      endParts.minutes > startParts.minutes &&
      endParts.minutes <= band[1];

    if (fits) {
      return {
        date: localDate(slotStart),
        start: hhmm(slotStart.getHours() * 60 + slotStart.getMinutes()),
        end: hhmm(slotEnd.getHours() * 60 + slotEnd.getMinutes()),
      };
    }
  }

  return { date: localDate(now), start: '09:00', end: '10:00' };
}

function toBookingSpace(space: SpaceDto): BookingSpace {
  const detail = [space.building, `${space.capacity} posti`].filter(Boolean).join(' · ');
  return {
    id: space.id,
    name: space.name,
    detail,
    equipment: space.equipment ?? [],
    capacity: space.capacity,
    mode: space.mode,
  };
}

function seatsLabel(count: number): string {
  return `${count} ${count === 1 ? 'posto' : 'posti'}`;
}

function peopleLabel(count: number): string {
  return `${count} ${count === 1 ? 'persona' : 'persone'}`;
}

// A mixed space shares its seats: the group must fit in the smallest number of free seats
// across the requested interval. Gaps between slots mean the space is completely free.
function minimumFreeSeats(
  availability: SpaceAvailabilityDto,
  startIso: string,
  endIso: string,
  fallbackCapacity: number,
): number | null {
  const startMs = Date.parse(startIso);
  const endMs = Date.parse(endIso);
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs <= startMs) return null;

  const capacity = availability.capacity > 0 ? availability.capacity : fallbackCapacity;
  const slots = (availability.slots ?? [])
    .map((slot) => ({
      start: Date.parse(slot.start),
      end: Date.parse(slot.end),
      freeSeats: Math.max(0, slot.freeSeats),
      isAvailable: slot.isAvailable,
    }))
    .filter((slot) => Number.isFinite(slot.start) && Number.isFinite(slot.end) && slot.start < slot.end);

  // A single full-day block means Bookings could not break the day down: let the server decide.
  if (
    slots.length === 1 &&
    !slots[0].isAvailable &&
    slots[0].end - slots[0].start >= 24 * 3_600_000
  ) {
    return null;
  }

  const boundaries = new Set<number>([startMs, endMs]);
  for (const slot of slots) {
    if (slot.end > startMs && slot.start < endMs) {
      boundaries.add(Math.max(startMs, slot.start));
      boundaries.add(Math.min(endMs, slot.end));
    }
  }

  const ordered = Array.from(boundaries).sort((a, b) => a - b);
  let freeSeats = capacity;
  for (let index = 0; index < ordered.length - 1; index += 1) {
    const from = ordered[index];
    const to = ordered[index + 1];
    if (from >= to) continue;
    const covering = slots.find((slot) => slot.start <= from && slot.end >= to);
    freeSeats = Math.min(freeSeats, covering ? covering.freeSeats : capacity);
  }

  return freeSeats;
}

@Component({
  selector: 'app-booking-form',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSliderModule,
    ReactiveFormsModule,
  ],
  templateUrl: './booking-form.component.html',
  styleUrl: './booking-form.component.scss',
})
export class BookingFormComponent {
  private readonly http = inject(HttpClient);
  private readonly spacesApi = inject(SpacesApiService);

  protected readonly session = inject(AuthSessionService);

  protected readonly bookingSpaces = signal<BookingSpace[]>([]);
  protected readonly bookingSpacesLoading = signal(true);
  protected readonly bookingSpacesError = signal('');
  protected readonly equipmentCatalog = signal<EquipmentCatalogItemDto[]>([]);

  protected readonly participantSeparators = [ENTER, COMMA];
  protected readonly participantEmails = signal<string[]>([]);
  protected readonly participantError = signal('');
  protected readonly emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  private readonly initialSlot = defaultBookingSlot();

  protected readonly bookingForm = new FormGroup({
    spaceId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    date: new FormControl(this.initialSlot.date, { nonNullable: true, validators: [Validators.required] }),
    start: new FormControl(this.initialSlot.start, { nonNullable: true, validators: [Validators.required] }),
    end: new FormControl(this.initialSlot.end, { nonNullable: true, validators: [Validators.required] }),
    equipment: new FormControl<string[]>([], { nonNullable: true }),
  });

  protected readonly minimumBookingDate = this.todayLocal();
  protected readonly maximumBookingDate = addDays(this.minimumBookingDate, MAX_ADVANCE_DAYS);
  protected readonly participantTotal = computed(() => 1 + this.participantEmails().length);

  // The slider reads the duration from the form values, so typing a start or end time moves it.
  private readonly bookingFormValue = toSignal(this.bookingForm.valueChanges, {
    initialValue: this.bookingForm.getRawValue(),
  });
  protected readonly bookingDuration = computed(() => {
    const { date, start, end } = this.bookingFormValue();
    if (!date || !start || !end) return 60;
    const minutes =
      (new Date(`${date}T${end}`).getTime() - new Date(`${date}T${start}`).getTime()) / 60000;
    return Number.isFinite(minutes) && minutes >= 30 && minutes <= 240 ? minutes : 60;
  });

  // The longest booking that still ends inside the band of the chosen start: the slider stops there.
  protected readonly maxBookingDuration = computed(() => {
    const startDate = this.bookingStartDate();
    if (!startDate) return 240;
    const campusStart = campusTimeParts(startDate).minutes;
    const band = BOOKING_BANDS.find(
      ([from, to]) => campusStart >= from && campusStart < to,
    );
    const bandEnd = band?.[1] ?? BOOKING_BANDS[BOOKING_BANDS.length - 1][1];
    return Math.max(30, Math.min(240, bandEnd - campusStart));
  });

  private readonly selectedSpaceId = toSignal(this.bookingForm.controls.spaceId.valueChanges, {
    initialValue: this.bookingForm.controls.spaceId.value,
  });
  protected readonly selectedBookingSpace = computed(
    () => this.bookingSpaces().find((space) => space.id === this.selectedSpaceId()) ?? null,
  );

  private readonly mixedFreeSeats = signal<number | null>(null);
  private mixedSeatsRequest = 0;

  protected readonly seatSummary = computed(() => {
    const space = this.selectedBookingSpace();
    if (!space) return '';
    if (space.mode === 'Dedicated') {
      return `Capienza: ${seatsLabel(space.capacity)}. La prenotazione occupa tutta l’aula.`;
    }
    const freeSeats = this.mixedFreeSeats();
    if (freeSeats === null) return `Capienza: ${seatsLabel(space.capacity)}.`;
    return `Posti liberi nella fascia: ${freeSeats} su ${space.capacity}.`;
  });

  protected readonly participantSeatWarning = computed(() => {
    const space = this.selectedBookingSpace();
    if (!space) return '';

    const total = this.participantTotal();
    if (space.mode === 'Dedicated') {
      if (total <= space.capacity) return '';
      return `Il gruppo è di ${peopleLabel(total)}, ma ${space.name} ha ${seatsLabel(space.capacity)}. Riduci i partecipanti o scegli uno spazio più grande.`;
    }

    const freeSeats = this.mixedFreeSeats();
    if (freeSeats === null || total <= freeSeats) return '';
    const verb = freeSeats === 1 ? 'resta' : 'restano';
    return `Nella fascia scelta ${verb} ${seatsLabel(freeSeats)} su ${space.capacity}, ma il gruppo è di ${peopleLabel(total)}. Riduci i partecipanti o cambia orario.`;
  });

  protected readonly participantMixedSizeWarning = computed(() => {
    const space = this.selectedBookingSpace();
    if (space?.mode !== 'Mixed') return '';
    const total = this.participantTotal();
    if (total <= MAX_MIXED_GROUP_SIZE) return '';
    return `In un’aula mista un gruppo può avere al massimo ${peopleLabel(MAX_MIXED_GROUP_SIZE)}. Riduci i partecipanti o prenota un’aula dedicata.`;
  });

  protected readonly bookingState = signal<'idle' | 'submitting' | 'success' | 'error'>('idle');
  protected readonly bookingMessage = signal('');
  protected readonly bookingReference = signal('');
  protected readonly bookingFieldErrors = signal<Partial<Record<BookingField, string>>>({});
  protected readonly equipmentError = signal('');

  constructor() {
    this.bookingForm.valueChanges.subscribe(() => this.clearBookingErrors());
    merge(
      this.bookingForm.controls.spaceId.valueChanges,
      this.bookingForm.controls.date.valueChanges,
      this.bookingForm.controls.start.valueChanges,
      this.bookingForm.controls.end.valueChanges,
    ).subscribe(() => this.refreshMixedSeats());
    if (!this.session.isAdmin()) {
      this.loadBookingSpaces();
      this.loadEquipmentCatalog();
    }
  }

  protected submitBooking(): void {
    this.setBookingFieldErrors({});
    this.participantError.set('');
    this.equipmentError.set('');
    this.bookingForm.markAllAsTouched();

    const errors = this.validateBooking();
    if (Object.keys(errors).length > 0) {
      this.setBookingFieldErrors(errors);
      this.bookingState.set('error');
      this.bookingMessage.set(Object.values(errors)[0] ?? 'Controlla i campi evidenziati.');
      return;
    }

    const seatWarning = this.participantSeatWarning();
    if (seatWarning) {
      this.bookingState.set('error');
      this.bookingMessage.set(seatWarning);
      return;
    }

    const mixedSizeWarning = this.participantMixedSizeWarning();
    if (mixedSizeWarning) {
      this.bookingState.set('error');
      this.bookingMessage.set(mixedSizeWarning);
      return;
    }

    const { date, start, end, spaceId, equipment } = this.bookingForm.getRawValue();
    const isGroup = this.participantEmails().length > 0;
    if (!isGroup && equipment.length > 0) {
      this.equipmentError.set('Gli strumenti si possono richiedere solo per una prenotazione di gruppo.');
      this.bookingState.set('error');
      this.bookingMessage.set('Aggiungi almeno un partecipante oppure togli gli strumenti richiesti.');
      return;
    }

    this.bookingState.set('submitting');
    this.bookingMessage.set('Invio della prenotazione in corso…');
    this.bookingReference.set('');

    this.http
      .post<BookingResponse>(
        '/api/bookings',
        {
          spaceId,
          start: new Date(`${date}T${start}`).toISOString(),
          end: new Date(`${date}T${end}`).toISOString(),
          participantEmails: this.participantEmails(),
          equipmentRequests: isGroup ? equipment.map((code) => ({ equipmentType: code })) : [],
        },
        { headers: this.bookingHeaders() },
      )
      .subscribe({
        next: (booking) => {
          this.bookingState.set('success');
          this.bookingReference.set(booking?.referenceCode ?? '');
          this.bookingMessage.set('Prenotazione confermata.');
        },
        error: (error: HttpErrorResponse) => {
          const payload = (error.error ?? {}) as Record<string, any>;
          this.bookingState.set('error');
          const validationMessage = this.applyBookingValidation(payload);
          this.bookingMessage.set(validationMessage ?? this.bookingErrorMessage(error.status, payload));
        },
      });
  }

  protected addParticipant(event: MatChipInputEvent): void {
    const email = event.value.trim().toLowerCase();
    event.chipInput?.clear();
    this.participantError.set('');
    if (!email) return;
    if (!this.emailPattern.test(email)) {
      this.participantError.set('Inserisci un indirizzo email valido.');
      return;
    }
    const ownEmail = this.session.user()?.email?.trim().toLowerCase();
    if (ownEmail && email === ownEmail) {
      this.participantError.set('Sei tu il responsabile della prenotazione: non aggiungere la tua email.');
      return;
    }
    if (this.participantEmails().includes(email)) {
      this.participantError.set('Questa email è già stata aggiunta.');
      return;
    }
    this.participantEmails.update((emails) => [...emails, email]);
    this.clearSeatBookingError();
  }

  protected removeParticipant(email: string): void {
    this.participantEmails.update((emails) => emails.filter((item) => item !== email));
    this.participantError.set('');
    if (this.participantEmails().length === 0) this.bookingForm.controls.equipment.setValue([]);
    this.clearSeatBookingError();
  }

  private clearSeatBookingError(): void {
    if (this.bookingState() !== 'error' || this.participantSeatWarning()) return;
    if (this.participantMixedSizeWarning()) return;
    this.bookingState.set('idle');
    this.bookingMessage.set('');
  }

  protected participantSummary(): string {
    const guests = this.participantEmails().length;
    if (guests === 0) return 'Partecipanti totali: 1 (solo il responsabile).';
    const invited = guests === 1 ? 'invitato' : 'invitati';
    return `Partecipanti totali: ${guests + 1} (responsabile + ${guests} ${invited}).`;
  }

  protected bookingFieldError(field: BookingField): string | null {
    return this.bookingFieldErrors()[field] ?? null;
  }

  protected equipmentForSelectedSpace(): EquipmentOption[] {
    const selected = this.bookingSpaces().find(
      (space) => space.id === this.bookingForm.controls.spaceId.value,
    );
    const present = new Set(selected?.equipment ?? []);
    return this.equipmentCatalog()
      .filter((item) => item.isStandard)
      .map((item) => ({
        code: item.code,
        name: item.name,
        available: present.has(item.code),
      }));
  }

  protected isEquipmentSelected(code: string): boolean {
    return this.bookingForm.controls.equipment.value.includes(code);
  }

  protected toggleEquipment(code: string, checked: boolean): void {
    const current = this.bookingForm.controls.equipment.value;
    const next = checked
      ? Array.from(new Set([...current, code]))
      : current.filter((item) => item !== code);
    this.bookingForm.controls.equipment.setValue(next);
    this.equipmentError.set('');
  }

  // The slider calls this while dragging too, so the duration and the end time follow the thumb.
  protected setBookingDuration(minutes: number): void {
    const startDate = this.bookingStartDate();
    if (!startDate) return;
    const duration = Math.min(Math.max(30, Math.round(minutes / 15) * 15), this.maxBookingDuration());
    const endDate = new Date(startDate.getTime() + duration * 60000);
    this.bookingForm.controls.end.setValue(hhmm(endDate.getHours() * 60 + endDate.getMinutes()));
  }

  private bookingStartDate(): Date | null {
    const { date, start } = this.bookingFormValue();
    if (!date || !start) return null;
    const startDate = new Date(`${date}T${start}`);
    return Number.isNaN(startDate.valueOf()) ? null : startDate;
  }

  protected retryBookingSpaces(): void {
    this.bookingSpacesLoading.set(true);
    this.bookingSpacesError.set('');
    this.loadBookingSpaces();
  }

  private loadBookingSpaces(): void {
    this.spacesApi.searchSpaces().subscribe({
      next: (spaces) => {
        const active = spaces.filter((space) => space.isActive).map(toBookingSpace);
        this.bookingSpaces.set(active);
        this.bookingSpacesLoading.set(false);
        if (active.length === 0) {
          this.bookingSpacesError.set('Nessuno spazio è prenotabile in questo momento.');
          return;
        }
        const selected = this.bookingForm.controls.spaceId.value;
        if (!active.some((space) => space.id === selected)) {
          this.bookingForm.controls.spaceId.setValue(active[0].id);
        } else {
          this.refreshMixedSeats();
        }
      },
      error: () => {
        this.bookingSpacesLoading.set(false);
        this.bookingSpacesError.set('Non è stato possibile caricare gli spazi. Riprova più tardi.');
      },
    });
  }

  private loadEquipmentCatalog(): void {
    this.spacesApi.getEquipmentCatalog().subscribe({
      next: (catalog) =>
        this.equipmentCatalog.set(catalog.length > 0 ? catalog : FALLBACK_EQUIPMENT),
      error: () => this.equipmentCatalog.set(FALLBACK_EQUIPMENT),
    });
  }

  private refreshMixedSeats(): void {
    const space = this.selectedBookingSpace();
    const { date, start, end } = this.bookingForm.getRawValue();
    const startDate = new Date(`${date}T${start}`);
    const endDate = new Date(`${date}T${end}`);

    if (
      !space ||
      space.mode !== 'Mixed' ||
      !date ||
      !start ||
      !end ||
      Number.isNaN(startDate.valueOf()) ||
      Number.isNaN(endDate.valueOf()) ||
      endDate <= startDate
    ) {
      this.mixedSeatsRequest += 1;
      this.mixedFreeSeats.set(null);
      return;
    }

    const requestId = ++this.mixedSeatsRequest;
    this.spacesApi.getSpaceDailyAvailability(space.id, date).subscribe({
      next: (availability) => {
        if (requestId !== this.mixedSeatsRequest) return;
        this.mixedFreeSeats.set(
          minimumFreeSeats(
            availability,
            startDate.toISOString(),
            endDate.toISOString(),
            space.capacity,
          ),
        );
      },
      error: () => {
        if (requestId === this.mixedSeatsRequest) this.mixedFreeSeats.set(null);
      },
    });
  }

  private validateBooking(): Partial<Record<BookingField, string>> {
    const { spaceId, date, start, end } = this.bookingForm.getRawValue();
    const errors: Partial<Record<BookingField, string>> = {};

    if (!spaceId) errors.spaceId = 'Scegli uno spazio.';
    if (!date) errors.date = 'Scegli una data.';
    if (!start) errors.start = 'Scegli l’orario di inizio.';
    if (!end) errors.end = 'Scegli l’orario di fine.';
    if (!date || !start || !end) return errors;

    const startDate = new Date(`${date}T${start}`);
    const endDate = new Date(`${date}T${end}`);
    if (Number.isNaN(startDate.valueOf())) errors.start = 'L’orario di inizio non è valido.';
    if (Number.isNaN(endDate.valueOf())) errors.end = 'L’orario di fine non è valido.';
    if (errors.start || errors.end) return errors;

    if (startDate <= new Date()) {
      errors.start = 'L’inizio deve essere nel futuro.';
    } else if (startDate.getTime() > Date.now() + MAX_ADVANCE_DAYS * 86400000) {
      errors.date = 'Puoi prenotare al massimo 15 giorni in anticipo.';
    }

    if (startDate.getMinutes() % 15 !== 0 || startDate.getSeconds() !== 0) {
      errors.start = 'L’orario di inizio deve essere multiplo di 15 minuti.';
    }

    const durationMinutes = (endDate.getTime() - startDate.getTime()) / 60000;
    if (durationMinutes <= 0) {
      errors.end = 'La fine deve essere dopo l’inizio.';
    } else if (durationMinutes < 30 || durationMinutes > 240) {
      errors.end = 'La durata deve essere tra 30 minuti e 4 ore.';
    } else if (durationMinutes % 15 !== 0) {
      errors.end = 'La fine deve essere a un multiplo di 15 minuti.';
    }

    const bandError = this.timeBandError(startDate, endDate);
    if (bandError && !errors.start) errors.start = bandError;

    return errors;
  }

  private timeBandError(start: Date, end: Date): string | null {
    const startParts = campusTimeParts(start);
    const endParts = campusTimeParts(end);
    const band = BOOKING_BANDS.find(
      ([from, to]) => startParts.minutes >= from && startParts.minutes < to,
    );
    const fits =
      band !== undefined &&
      endParts.date === startParts.date &&
      endParts.minutes > startParts.minutes &&
      endParts.minutes <= band[1];

    return fits
      ? null
      : 'La prenotazione deve stare in una sola fascia: mattina 08:00–14:00, pomeriggio 14:00–19:00, sera 19:00–23:00.';
  }

  private bookingHeaders(): HttpHeaders {
    const token = this.readBookingToken();
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }

  // Login, token storage and the HTTP interceptor arrive with #82; until then the form reads
  // the token from sessionStorage, under the key documented in the PR.
  private readBookingToken(): string | null {
    try {
      return window.sessionStorage.getItem(BOOKING_TOKEN_STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private clearBookingErrors(): void {
    if (Object.keys(this.bookingFieldErrors()).length > 0) this.setBookingFieldErrors({});
    if (this.participantError()) this.participantError.set('');
    if (this.equipmentError()) this.equipmentError.set('');
    if (this.bookingState() === 'error') {
      this.bookingState.set('idle');
      this.bookingMessage.set('');
    }
  }

  private setBookingFieldErrors(errors: Partial<Record<BookingField, string>>): void {
    this.bookingFieldErrors.set(errors);
    for (const field of ['spaceId', 'date', 'start', 'end'] as const) {
      const control = this.bookingForm.controls[field];
      const message = errors[field];
      if (message) {
        control.setErrors({ ...control.errors, booking: message });
        control.markAsTouched();
      } else if (control.hasError('booking')) {
        const remaining = { ...control.errors };
        delete remaining['booking'];
        control.setErrors(Object.keys(remaining).length > 0 ? remaining : null);
      }
    }
  }

  private bookingErrorMessage(status: number, payload: Record<string, any>): string {
    const title = String(payload['title'] ?? '');
    if (status === 401) return 'Devi effettuare l’accesso per confermare una prenotazione.';
    if (status === 0) return 'Il servizio prenotazioni non è raggiungibile. Riprova tra poco.';
    if (title === 'Space already booked') return 'Questa fascia è già occupata. Scegli un altro orario.';
    if (title === 'Time band already used') return 'Hai già una prenotazione in questa fascia della giornata. Scegli un altro orario.';
    if (title === 'Not enough seats') return 'Non ci sono abbastanza posti liberi in questa fascia.';
    if (title === 'Group too large')
      return `In un’aula mista un gruppo può avere al massimo ${peopleLabel(MAX_MIXED_GROUP_SIZE)}. Riduci i partecipanti o prenota un’aula dedicata.`;
    if (title === 'Overlapping booking') return 'Hai già una prenotazione sovrapposta in questa fascia.';
    if (title === 'Closing comment pending') return 'Prima di prenotare devi completare il commento finale della prenotazione.';
    if (title === 'Space not bookable') return 'Questo spazio non è prenotabile in questa fascia.';
    if (title === 'Space not found') return 'Lo spazio selezionato non esiste più: aggiorna la pagina e riprova.';
    if (title === 'Spaces unavailable' || title === 'Users unavailable') return 'Un servizio necessario non è raggiungibile. Riprova tra poco.';
    return String(payload['detail'] ?? 'Non è stato possibile creare la prenotazione.');
  }

  private applyBookingValidation(payload: Record<string, any>): string | null {
    const errors = payload['errors'] as Record<string, string[]> | undefined;
    if (!errors) return null;

    const fieldErrors: Partial<Record<BookingField, string>> = {};
    let firstMessage: string | null = null;

    for (const [name, messages] of Object.entries(errors)) {
      const message = messages?.[0];
      if (!message) continue;
      firstMessage ??= message;

      switch (name.toLowerCase()) {
        case 'start':
          fieldErrors.start = message;
          break;
        case 'end':
          fieldErrors.end = message;
          break;
        case 'spaceid':
          fieldErrors.spaceId = message;
          break;
        case 'participantemails':
          this.participantError.set(message);
          break;
        case 'equipmentrequests':
          this.equipmentError.set(message);
          break;
      }
    }

    this.setBookingFieldErrors(fieldErrors);
    return firstMessage;
  }

  private todayLocal(): string {
    const now = new Date();
    return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
  }
}
