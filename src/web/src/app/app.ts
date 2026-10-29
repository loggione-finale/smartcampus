import { Component, ElementRef, afterNextRender, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { BookingFormComponent } from './bookings/booking-form.component';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { filter, map } from 'rxjs';
import { AuthSessionService } from './auth/auth-session.service';
import { safeReturnUrl } from './auth/return-url';

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

type RegistrationResult = 'created' | null;

type RegisterRequest = {
  matricola: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  password: string;
};

type RegistrationField = keyof RegisterRequest;

@Component({
  selector: 'app-root',
  imports: [
    BookingFormComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    ReactiveFormsModule,
    RouterLink,
    RouterOutlet,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  protected readonly session = inject(AuthSessionService);

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly showHome = computed(() => {
    const path = this.currentUrl().split(/[?#]/, 1)[0];
    return path === '' || path === '/';
  });

  protected readonly navLinks = [
    { label: 'Come funziona', href: '/#come-funziona', icon: 'route' },
    { label: 'Spazi', href: '/#spazi', icon: 'meeting_room' },
    { label: 'Prenota', href: '/#prenota', icon: 'event_available' },
    { label: 'Servizi', href: '/#servizi', icon: 'hub' },
    { label: 'Registrati', href: '/#registrazione', icon: 'person_add' },
    { label: 'Contatti', href: '/#contatti', icon: 'mail' },
  ];

  protected readonly heroWords = ['Cerca.', 'Prenota.', 'Studia.'];

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
  protected readonly registrationReturnUrl = signal<string | null>(null);
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
    this.registrationReturnUrl.set(
      new URLSearchParams(window.location.search).get('returnUrl'),
    );
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

    this.http.post('/api/users/auth/register', request).subscribe({
      next: () => {
        this.registrationSubmitting.set(false);
        // Users arriving from the registration link in an invitation email return
        // to the invitation after signing up (the /invitations route is protected:
        // if not logged in yet, they pass through login with returnUrl).
        // safeReturnUrl scarta i percorsi esterni (//host, backslash, /login).
        const returnUrl = safeReturnUrl(this.registrationReturnUrl());
        if (returnUrl) {
          void this.router.navigateByUrl(returnUrl);
          return;
        }
        this.registrationResult.set('created');
        const confirmationId = 'conferma-registrazione';
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

  protected logout(): void {
    this.session.clear();
    void this.router.navigate(['/login'], { queryParams: { reason: 'logout' } });
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
