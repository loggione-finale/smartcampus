import { Component, ElementRef, afterNextRender, inject, signal } from '@angular/core';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { RouterOutlet } from '@angular/router';
import { SpacesApiService } from './spaces/spaces-api.service';
import { EquipmentCatalogItemDto, SpaceDto } from './spaces/spaces.models';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

type Health = 'checking' | 'online' | 'offline';

type Service = {
  name: string;
  summary: string;
  features: string[];
  healthUrl: string;
  icon: string;
  health: Health;
};

type Space = {
  name: string;
  where: string;
  facts: string[];
  image: string;
  alt: string;
};

type RegistrationResult = 'created' | 'invite' | null;

type RegisterRequest = {
  matricola: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  password: string;
  inviteToken?: string;
};

type RegistrationField = Exclude<keyof RegisterRequest, 'inviteToken'>;

type BookingSpace = {
  id: string;
  name: string;
  detail: string;
  equipment: string[];
};

type EquipmentOption = {
  code: string;
  name: string;
  available: boolean;
};

type BookingField = 'spaceId' | 'date' | 'start' | 'end';

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

function localDate(date: Date): string {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
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
  };
}

@Component({
  selector: 'app-root',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatSliderModule,
    ReactiveFormsModule,
    RouterOutlet,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly spacesApi = inject(SpacesApiService);

  protected readonly navLinks = [
    { label: 'Come funziona', href: '#come-funziona', icon: 'route' },
    { label: 'Spazi', href: '#spazi', icon: 'meeting_room' },
    { label: 'Prenota', href: '#prenota', icon: 'event_available' },
    { label: 'Servizi', href: '#servizi', icon: 'hub' },
    { label: 'Registrati', href: '#registrazione', icon: 'person_add' },
    { label: 'Contatti', href: '#contatti', icon: 'mail' },
  ];

  protected readonly heroWords = ['Cerca.', 'Prenota.', 'Studia.'];

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

  protected readonly bookingState = signal<'idle' | 'submitting' | 'success' | 'error'>('idle');
  protected readonly bookingMessage = signal('');
  protected readonly bookingReference = signal('');
  protected readonly bookingFieldErrors = signal<Partial<Record<BookingField, string>>>({});
  protected readonly equipmentError = signal('');

  protected readonly steps = [
    { title: 'Cerca', text: 'lo spazio giusto per posti, orari e attrezzature.' },
    { title: 'Prenota', text: 'la fascia oraria: il sistema blocca le sovrapposizioni.' },
    { title: 'Studia', text: 'con il tuo profilo e il tuo ruolo già al loro posto.' },
  ];

  // shortcut: sample catalogue, replace with data from the Spaces service once its API exists
  protected readonly spaces: Space[] = [
    {
      name: 'Sala lettura',
      where: 'Biblioteca centrale',
      facts: ['180 posti', 'Silenzio', '8:00 – 23:00'],
      image: 'assets/graz-university-library-reading-room.jpg',
      alt: 'Una sala lettura storica con lunghi tavoli e scaffali alle pareti',
    },
    {
      name: 'Aula magna B',
      where: 'Edificio didattico',
      facts: ['240 posti', 'Proiettore', 'Lezioni ed esami'],
      image: 'assets/students-at-lecture-hall.jpg',
      alt: 'Studenti seduti a gradoni in un’aula universitaria',
    },
    {
      name: 'Laboratorio 2',
      where: 'Polo informatico',
      facts: ['40 postazioni', 'Linux e Windows', 'Su turni'],
      image: 'assets/computer-lab-at-the-university-of-plymouth.jpg',
      alt: 'Un laboratorio informatico con file di computer',
    },
    {
      name: 'Sala studio Nord',
      where: 'Piano terra',
      facts: ['60 posti', 'Prese a ogni tavolo', 'Fino a mezzanotte'],
      image: 'assets/suzzallo-reading-room-university-of-washington-restoration.jpg',
      alt: 'Una grande sala studio con volte alte e lampade da tavolo',
    },
  ];

  protected readonly services = signal<Service[]>([
    {
      name: 'Spaces',
      summary: 'Il catalogo del campus.',
      features: ['Aule e laboratori', 'Sale studio', 'Posti e attrezzature'],
      healthUrl: '/api/spaces/health',
      icon: 'meeting_room',
      health: 'checking',
    },
    {
      name: 'Bookings',
      summary: 'Le prenotazioni, senza conflitti.',
      features: ['Fasce orarie', 'Controllo sovrapposizioni', 'Storico prenotazioni'],
      healthUrl: '/api/bookings/health',
      icon: 'event_available',
      health: 'checking',
    },
    {
      name: 'Users',
      summary: 'Chi entra e con quale ruolo.',
      features: ['Profili sintetici', 'Studenti e docenti', 'Ruoli del campus'],
      healthUrl: '/api/users/health',
      icon: 'badge',
      health: 'checking',
    },
  ]);

  protected readonly healthLabel: Record<Health, string> = {
    checking: 'Verifica in corso',
    online: 'Online',
    offline: 'Non raggiungibile',
  };

  protected readonly year = new Date().getFullYear();
  protected readonly minimumBookingDate = this.todayLocal();
  protected readonly inviteToken = signal<string | null>(null);
  protected readonly registrationResult = signal<RegistrationResult>(null);
  protected readonly registrationSubmitting = signal(false);
  protected readonly registrationError = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly registrationForm = this.formBuilder.nonNullable.group({
    matricola: ['', [Validators.required, Validators.pattern(/^\d{10}$/)]],
    firstName: [
      '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(50), Validators.pattern(/^[\p{L}][\p{L} '-]*$/u)],
    ],
    lastName: [
      '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(50), Validators.pattern(/^[\p{L}][\p{L} '-]*$/u)],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(/^\+39\d{9,10}$/)]],
    password: ['', [Validators.required, Validators.minLength(10), Validators.pattern(/^(?=.*[\p{L}])(?=.*\d).+$/u)]],
  });

  constructor() {
    this.inviteToken.set(new URLSearchParams(window.location.search).get('invite'));
    this.loadBookingSpaces();
    this.loadEquipmentCatalog();
    this.bookingForm.valueChanges.subscribe(() => this.clearBookingErrors());
    afterNextRender(() => {
      this.checkHealth();
      this.revealOnScroll();
    });
  }

  protected fieldError(field: RegistrationField): string | null {
    const control = this.registrationForm.controls[field];
    if (control.hasError('required')) return 'Campo obbligatorio.';
    if (control.hasError('minlength')) return `Inserisci almeno ${control.getError('minlength').requiredLength} caratteri.`;
    if (control.hasError('maxlength')) return `Puoi inserire al massimo ${control.getError('maxlength').requiredLength} caratteri.`;
    if (control.hasError('email')) return 'Inserisci un indirizzo email valido.';
    if (control.hasError('pattern')) {
      const messages: Partial<Record<RegistrationField, string>> = {
        matricola: 'La matricola ha 10 cifre.',
        firstName: 'Usa lettere, spazi, apostrofi o trattini.',
        lastName: 'Usa lettere, spazi, apostrofi o trattini.',
        phoneNumber: 'Usa il formato italiano, ad esempio +393331234567.',
        password: 'La password deve contenere almeno una lettera e una cifra.',
      };
      return messages[field] ?? 'Formato non valido.';
    }
    return this.serverErrors()[field]?.[0] ?? null;
  }

  protected submitRegistration(): void {
    this.registrationError.set(null);
    this.clearServerErrors();

    if (this.registrationForm.invalid) {
      this.registrationForm.markAllAsTouched();
      return;
    }

    this.registrationSubmitting.set(true);
    const request: RegisterRequest = this.registrationForm.getRawValue();
    const inviteToken = this.inviteToken();
    const requestWithInvite = inviteToken ? { ...request, inviteToken } : request;

    this.http.post('/api/users/auth/register', requestWithInvite).subscribe({
      next: () => {
        this.registrationSubmitting.set(false);
        const result = this.inviteToken() ? 'invite' : 'created';
        this.registrationResult.set(result);
        const confirmationId = result === 'invite' ? 'conferma-invito' : 'conferma-registrazione';
        window.history.replaceState({}, '', `${window.location.pathname}${window.location.search}#${confirmationId}`);
        requestAnimationFrame(() => document.getElementById(confirmationId)?.scrollIntoView({ behavior: 'smooth' }));
      },
      error: (error: HttpErrorResponse) => this.handleRegistrationError(error),
    });
  }

  protected resetRegistration(): void {
    this.registrationForm.reset();
    this.registrationResult.set(null);
    this.registrationError.set(null);
    this.serverErrors.set({});
  }

  private handleRegistrationError(error: HttpErrorResponse): void {
    this.registrationSubmitting.set(false);
    const payload = error.error as { errors?: Record<string, string[]>; detail?: string } | null;

    if (error.status === 400 && payload?.errors) {
      const errors = Object.entries(payload.errors).reduce<Record<string, string[]>>((mapped, [field, messages]) => {
        const normalizedField = field.charAt(0).toLowerCase() + field.slice(1);
        mapped[normalizedField] = messages;
        return mapped;
      }, {});
      this.setServerErrors(errors);
      this.registrationError.set('Controlla i campi evidenziati.');
      return;
    }

    if (error.status === 409) {
      const detail = payload?.detail ?? 'I dati inseriti risultano già registrati.';
      const field = detail.toLowerCase().includes('matricola') ? 'matricola' : 'email';
      this.setServerErrors({ [field]: [detail] });
      this.registrationError.set('Esiste già un account con uno dei dati inseriti.');
      return;
    }

    this.registrationError.set('Non è stato possibile completare la registrazione. Riprova tra poco.');
  }

  private clearServerErrors(): void {
    this.serverErrors.set({});
    for (const control of Object.values(this.registrationForm.controls)) {
      if (!control.hasError('server')) continue;
      const errors = { ...control.errors };
      delete errors['server'];
      control.setErrors(Object.keys(errors).length > 0 ? errors : null);
    }
  }

  private setServerErrors(errors: Record<string, string[]>): void {
    this.serverErrors.set(errors);
    for (const [field, messages] of Object.entries(errors)) {
      const control = this.registrationForm.get(field);
      if (control) {
        control.setErrors({ ...control.errors, server: messages[0] });
        control.markAsTouched();
      }
    }
  }

  private checkHealth(): void {
    for (const service of this.services()) {
      this.http.get(service.healthUrl, { observe: 'response' }).subscribe({
        next: () => this.setServiceHealth(service.name, 'online'),
        error: () => this.setServiceHealth(service.name, 'offline'),
      });
    }
  }

  private setServiceHealth(name: string, health: Health): void {
    this.services.update((list) =>
      list.map((service) => (service.name === name ? { ...service, health } : service)),
    );
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
    if (this.participantEmails().includes(email)) {
      this.participantError.set('Questa email è già stata aggiunta.');
      return;
    }
    this.participantEmails.update((emails) => [...emails, email]);
  }

  protected removeParticipant(email: string): void {
    this.participantEmails.update((emails) => emails.filter((item) => item !== email));
    this.participantError.set('');
    if (this.participantEmails().length === 0) this.bookingForm.controls.equipment.setValue([]);
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

  protected bookingDuration(): number {
    const { date, start, end } = this.bookingForm.getRawValue();
    if (!date || !start || !end) return 60;
    const minutes =
      (new Date(`${date}T${end}`).getTime() - new Date(`${date}T${start}`).getTime()) / 60000;
    return Number.isFinite(minutes) && minutes >= 30 && minutes <= 240 ? minutes : 60;
  }

  protected setBookingDuration(minutes: number): void {
    const { date, start } = this.bookingForm.getRawValue();
    if (!date || !start) return;
    const startDate = new Date(`${date}T${start}`);
    if (Number.isNaN(startDate.valueOf())) return;

    const campusStart = campusTimeParts(startDate).minutes;
    const band = BOOKING_BANDS.find(
      ([from, to]) => campusStart >= from && campusStart < to,
    );
    const bandEnd = band?.[1] ?? BOOKING_BANDS[BOOKING_BANDS.length - 1][1];
    const maxDuration = Math.max(30, Math.min(240, bandEnd - campusStart));
    const duration = Math.min(Math.max(30, Math.round(minutes / 15) * 15), maxDuration);
    const endDate = new Date(startDate.getTime() + duration * 60000);
    this.bookingForm.controls.end.setValue(hhmm(endDate.getHours() * 60 + endDate.getMinutes()));
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

  // Content stays visible without script: only the .js class hides it before it enters.
  private revealOnScroll(): void {
    const root = this.host.nativeElement;
    if (!('IntersectionObserver' in window)) return;
    document.documentElement.classList.add('js');
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add('in');
            observer.unobserve(entry.target);
          }
        }
      },
      { rootMargin: '0px 0px -10% 0px' },
    );
    root.querySelectorAll('[data-reveal]').forEach((el) => observer.observe(el));
  }
}
