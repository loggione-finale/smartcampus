import { Component, ElementRef, afterNextRender, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';

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

@Component({
  selector: 'app-root',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly navLinks = [
    { label: 'Come funziona', href: '#come-funziona', icon: 'route' },
    { label: 'Spazi', href: '#spazi', icon: 'meeting_room' },
    { label: 'Servizi', href: '#servizi', icon: 'hub' },
    { label: 'Contatti', href: '#contatti', icon: 'mail' },
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
