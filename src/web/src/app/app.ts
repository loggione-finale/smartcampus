import { Component, ElementRef, afterNextRender, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { BookingTeaserComponent } from './bookings/booking-teaser.component';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { filter, map } from 'rxjs';
import { AuthSessionService } from './auth/auth-session.service';

type Space = {
  name: string;
  where: string;
  facts: string[];
  image: string;
  alt: string;
};

@Component({
  selector: 'app-root',
  imports: [
    BookingTeaserComponent,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    RouterLink,
    RouterOutlet,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
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
    { label: 'Prenota', href: '/book', icon: 'event_available' },
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

  protected readonly year = new Date().getFullYear();

  constructor() {
    afterNextRender(() => {
      this.revealOnScroll();
      // A /#section link from another page loads before the section exists: scroll once it does.
      if (this.showHome() && window.location.hash) {
        document.getElementById(decodeURIComponent(window.location.hash.slice(1)))?.scrollIntoView();
      }
    });
  }

  protected logout(): void {
    this.session.clear();
    void this.router.navigate(['/login'], { queryParams: { reason: 'logout' } });
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
