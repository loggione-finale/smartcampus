import { Component, ElementRef, afterNextRender, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

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

type BookingSpace = {
  id: string;
  name: string;
  detail: string;
};

type BookingState = 'idle' | 'submitting' | 'success' | 'error';

@Component({
  selector: 'app-root',
  imports: [
    MatButtonModule,
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

  protected readonly navLinks = [
    { label: 'Come funziona', href: '#come-funziona', icon: 'route' },
    { label: 'Spazi', href: '#spazi', icon: 'meeting_room' },
    { label: 'Prenota', href: '#prenota', icon: 'event_available' },
    { label: 'Servizi', href: '#servizi', icon: 'hub' },
    { label: 'Contatti', href: '#contatti', icon: 'mail' },
  ];

  protected readonly heroWords = ['Cerca.', 'Prenota.', 'Studia.'];

  protected readonly bookingSpaces: BookingSpace[] = [
    { id: '3f2a9c1e-0000-4000-8000-000000000001', name: 'Aula 101', detail: 'Edificio A · 30 posti' },
    { id: '3f2a9c1e-0000-4000-8000-000000000002', name: 'Laboratorio Informatica 2', detail: 'Edificio B · 24 posti' },
    { id: '3f2a9c1e-0000-4000-8000-000000000003', name: 'Sala Studio Piano Terra', detail: 'Edificio A · 12 posti' },
    { id: '3f2a9c1e-0000-4000-8000-000000000004', name: 'Aula 204', detail: 'Edificio A · 6 posti' },
  ];

  protected readonly bookingForm = new FormGroup({
    spaceId: new FormControl(this.bookingSpaces[0].id, { nonNullable: true, validators: [Validators.required] }),
    date: new FormControl(this.todayLocal(), { nonNullable: true, validators: [Validators.required] }),
    start: new FormControl('09:00', { nonNullable: true, validators: [Validators.required] }),
    end: new FormControl('10:00', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly bookingState = signal<BookingState>('idle');
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

  constructor() {
    afterNextRender(() => {
      this.checkHealth();
      this.revealOnScroll();
    });
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
    if (this.bookingForm.invalid) {
      this.bookingState.set('error');
      this.bookingMessage.set('Controlla i campi obbligatori prima di inviare la prenotazione.');
      return;
    }

    const { date, start, end, spaceId } = this.bookingForm.getRawValue();
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
        }),
      });

      const payload = await this.readJson(response);
      if (response.ok) {
        this.bookingState.set('success');
        this.bookingReference.set(payload?.['referenceCode'] ?? '');
        this.bookingMessage.set('Prenotazione confermata.');
        return;
      }

      this.bookingState.set('error');
      this.bookingMessage.set(this.bookingErrorMessage(response.status, payload));
      this.applyServerValidation(payload);
    } catch {
      this.bookingState.set('error');
      this.bookingMessage.set('Il servizio prenotazioni non è raggiungibile. Riprova tra poco.');
    }
  }

  protected fieldError(name: 'spaceId' | 'date' | 'start' | 'end'): string {
    const control = this.bookingForm.controls[name];
    if (control.hasError('server')) return control.getError('server');
    if (control.hasError('required')) return 'Campo obbligatorio.';
    return '';
  }

  private async readJson(response: Response): Promise<Record<string, any>> {
    try {
      return (await response.json()) as Record<string, any>;
    } catch {
      return {};
    }
  }

  private bookingErrorMessage(status: number, payload: Record<string, any>): string {
    const title = String(payload['title'] ?? '');
    const detail = String(payload['detail'] ?? '');
    if (status === 401) return 'Devi effettuare l’accesso per confermare una prenotazione.';
    if (status === 400) return 'I dati della prenotazione non sono validi.';
    if (title === 'Space already booked') return 'Questa fascia è già occupata. Scegli un altro orario.';
    if (title === 'Not enough seats') return 'Non ci sono abbastanza posti liberi in questa fascia.';
    if (title === 'Overlapping booking') return 'Hai già una prenotazione sovrapposta in questa fascia.';
    if (title === 'Closing comment pending') {
      const code = payload['bookingToComment'] ? ` (${payload['bookingToComment']})` : '';
      return `Prima di prenotare devi completare il commento finale della prenotazione${code}.`;
    }
    if (status === 404) return 'Lo spazio selezionato non è più disponibile.';
    if (status === 503) return 'Il catalogo spazi non è momentaneamente disponibile.';
    return detail || 'Non è stato possibile creare la prenotazione.';
  }

  private applyServerValidation(payload: Record<string, any>): void {
    const errors = payload['errors'] as Record<string, string[]> | undefined;
    if (!errors) return;
    for (const [name, messages] of Object.entries(errors)) {
      const controlName = name.charAt(0).toLowerCase() + name.slice(1) as 'spaceId' | 'date' | 'start' | 'end';
      const control = this.bookingForm.get(controlName);
      if (control && messages[0]) control.setErrors({ server: messages[0] });
    }
  }

  private todayLocal(): string {
    const now = new Date();
    const offset = now.getTimezoneOffset() * 60000;
    return new Date(now.getTime() - offset).toISOString().slice(0, 10);
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
