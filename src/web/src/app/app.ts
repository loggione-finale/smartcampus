import { Component, ElementRef, afterNextRender, inject, signal } from '@angular/core';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
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
  name: string;
  available: boolean;
};

@Component({
  selector: 'app-root',
  imports: [
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
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

  protected readonly navLinks = [
    { label: 'Come funziona', href: '#come-funziona', icon: 'route' },
    { label: 'Spazi', href: '#spazi', icon: 'meeting_room' },
    { label: 'Prenota', href: '#prenota', icon: 'event_available' },
    { label: 'Servizi', href: '#servizi', icon: 'hub' },
    { label: 'Registrati', href: '#registrazione', icon: 'person_add' },
    { label: 'Contatti', href: '#contatti', icon: 'mail' },
  ];

  protected readonly heroWords = ['Cerca.', 'Prenota.', 'Studia.'];

  protected readonly bookingSpaces: BookingSpace[] = [
    { id: '3f2a9c1e-0000-4000-8000-000000000001', name: 'Aula 101', detail: 'Edificio A · 30 posti', equipment: ['Videoproiettore', 'Lavagna', 'Microfono'] },
    { id: '3f2a9c1e-0000-4000-8000-000000000002', name: 'Laboratorio Informatica 2', detail: 'Edificio B · 24 posti', equipment: ['Videoproiettore', 'Lavagna'] },
    { id: '3f2a9c1e-0000-4000-8000-000000000003', name: 'Sala Studio Piano Terra', detail: 'Edificio A · 12 posti', equipment: ['Lavagna'] },
    { id: '3f2a9c1e-0000-4000-8000-000000000004', name: 'Aula 204', detail: 'Edificio A · 6 posti', equipment: ['Videoproiettore'] },
  ];

  protected readonly participantSeparators = [ENTER, COMMA];
  protected readonly participantEmails = signal<string[]>([]);
  protected readonly participantError = signal('');
  protected readonly emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  protected readonly bookingForm = new FormGroup({
    spaceId: new FormControl(this.bookingSpaces[0].id, { nonNullable: true, validators: [Validators.required] }),
    date: new FormControl(this.todayLocal(), { nonNullable: true, validators: [Validators.required] }),
    start: new FormControl('09:00', { nonNullable: true, validators: [Validators.required] }),
    end: new FormControl('10:00', { nonNullable: true, validators: [Validators.required] }),
    equipment: new FormControl<string[]>([], { nonNullable: true }),
  });

  protected readonly bookingState = signal<'idle' | 'submitting' | 'success' | 'error'>('idle');
  protected readonly bookingMessage = signal('');
  protected readonly bookingReference = signal('');

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
      fetch(service.healthUrl)
        .then((res) => (res.ok ? 'online' : 'offline'))
        .catch(() => 'offline' as const)
        .then((health) =>
          this.services.update((list) =>
            list.map((s) => (s.name === service.name ? { ...s, health } : s)),
          ),
        );
    }
  }

  protected async submitBooking(): Promise<void> {
    this.bookingForm.markAllAsTouched();
    if (this.bookingForm.invalid || this.participantError()) {
      this.bookingState.set('error');
      this.bookingMessage.set('Controlla i campi obbligatori prima di inviare la prenotazione.');
      return;
    }

    const { date, start, end, spaceId, equipment } = this.bookingForm.getRawValue();
    const startDate = new Date(`${date}T${start}`);
    const endDate = new Date(`${date}T${end}`);
    if (Number.isNaN(startDate.valueOf()) || Number.isNaN(endDate.valueOf())) {
      this.bookingState.set('error');
      this.bookingMessage.set('Inserisci una data e un orario validi.');
      return;
    }

    this.bookingState.set('submitting');
    this.bookingMessage.set('Invio della prenotazione in corso…');
    this.bookingReference.set('');

    try {
      const response = await fetch('/api/bookings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          spaceId,
          start: startDate.toISOString(),
          end: endDate.toISOString(),
          participantEmails: this.participantEmails(),
          equipmentRequests: this.participantEmails().length > 0 ? equipment : [],
        }),
      });
      const payload = await this.readBookingJson(response);
      if (response.ok) {
        this.bookingState.set('success');
        this.bookingReference.set(payload['referenceCode'] ?? '');
        this.bookingMessage.set('Prenotazione confermata.');
        return;
      }
      this.bookingState.set('error');
      this.bookingMessage.set(this.bookingErrorMessage(response.status, payload));
      this.applyBookingValidation(payload);
    } catch {
      this.bookingState.set('error');
      this.bookingMessage.set('Il servizio prenotazioni non è raggiungibile. Riprova tra poco.');
    }
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

  protected equipmentForSelectedSpace(): EquipmentOption[] {
    const selected = this.bookingSpaces.find((space) => space.id === this.bookingForm.controls.spaceId.value);
    const available = new Set(selected?.equipment ?? []);
    return ['Videoproiettore', 'Lavagna', 'Microfono', 'Adattatore HDMI'].map((name) => ({
      name,
      available: available.has(name),
    }));
  }

  private async readBookingJson(response: Response): Promise<Record<string, any>> {
    try { return (await response.json()) as Record<string, any>; } catch { return {}; }
  }

  private bookingErrorMessage(status: number, payload: Record<string, any>): string {
    const title = String(payload['title'] ?? '');
    if (status === 401) return 'Devi effettuare l’accesso per confermare una prenotazione.';
    if (title === 'Space already booked') return 'Questa fascia è già occupata. Scegli un altro orario.';
    if (title === 'Not enough seats') return 'Non ci sono abbastanza posti liberi in questa fascia.';
    if (title === 'Overlapping booking') return 'Hai già una prenotazione sovrapposta in questa fascia.';
    if (title === 'Closing comment pending') return 'Prima di prenotare devi completare il commento finale della prenotazione.';
    return String(payload['detail'] ?? 'Non è stato possibile creare la prenotazione.');
  }

  private applyBookingValidation(payload: Record<string, any>): void {
    const errors = payload['errors'] as Record<string, string[]> | undefined;
    if (!errors) return;
    for (const [name, messages] of Object.entries(errors)) {
      const control = this.bookingForm.get(name.charAt(0).toLowerCase() + name.slice(1));
      if (control && messages[0]) control.setErrors({ server: messages[0] });
    }
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
