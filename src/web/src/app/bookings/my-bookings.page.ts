import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subscription, catchError, of, switchMap, throwError } from 'rxjs';
import { MyBookingsApiService } from './my-bookings-api.service';
import {
  BOOKING_STATUS_LABELS,
  MyBookingDto,
  MyInvitationDto,
  PARTICIPANT_ROLE_LABELS,
  formatBookingDate,
  formatBookingTimeRange,
  formatInvitationExpiry,
  splitBookings,
} from './my-bookings.models';

export const BOOKINGS_CHANGED_EVENT = 'smartcampus:bookings-changed';

@Component({
  selector: 'app-my-bookings-page',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './my-bookings.page.html',
  styleUrl: './my-bookings.page.scss',
})
export class MyBookingsPage implements OnInit {
  private readonly api = inject(MyBookingsApiService);
  private readonly destroyRef = inject(DestroyRef);

  private loadSubscription: Subscription | null = null;
  private acceptSubscription: Subscription | null = null;
  private cancelSubscription: Subscription | null = null;

  protected readonly statusLabels = BOOKING_STATUS_LABELS;
  protected readonly roleLabels = PARTICIPANT_ROLE_LABELS;

  protected readonly bookings = signal<MyBookingDto[]>([]);
  protected readonly invitations = signal<MyInvitationDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly actionSuccess = signal<string | null>(null);
  protected readonly invitationsUnavailable = signal(false);
  protected readonly acceptingInvitationId = signal<string | null>(null);
  protected readonly confirmingCancelBookingId = signal<string | null>(null);
  protected readonly cancellingBookingId = signal<string | null>(null);

  protected readonly sections = computed(() => {
    const { upcoming, past } = splitBookings(this.bookings());
    return [
      { key: 'upcoming', tag: 'IN ARRIVO', title: 'Prossime prenotazioni', bookings: upcoming },
      { key: 'past', tag: 'ARCHIVIO', title: 'Prenotazioni passate', bookings: past },
    ].filter((section) => section.bookings.length > 0);
  });

  protected readonly countLabel = computed(() => {
    const count = this.bookings().length;
    return count === 1 ? '1 prenotazione' : `${count} prenotazioni`;
  });

  protected readonly pendingInvitations = computed(() => {
    const byBookingId = new Map<string, MyInvitationDto>();
    for (const invitation of this.invitations()) {
      if (invitation.status === 'Invited') {
        byBookingId.set(invitation.bookingId, invitation);
      }
    }
    return byBookingId;
  });

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.loadSubscription?.unsubscribe();
      this.acceptSubscription?.unsubscribe();
      this.cancelSubscription?.unsubscribe();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected refresh(): void {
    this.actionError.set(null);
    this.actionSuccess.set(null);
    this.load();
  }

  protected pendingInvitation(booking: MyBookingDto): MyInvitationDto | null {
    return this.pendingInvitations().get(booking.id) ?? null;
  }

  protected bookingDate(booking: MyBookingDto): string {
    return formatBookingDate(booking.start);
  }

  protected bookingTime(booking: MyBookingDto): string {
    return formatBookingTimeRange(booking.start, booking.end);
  }

  protected invitationExpiry(booking: MyBookingDto): string | null {
    return formatInvitationExpiry(booking.invitationExpiresAt);
  }

  protected invitationExpired(booking: MyBookingDto): boolean {
    if (!booking.invitationExpiresAt) {
      return false;
    }

    const expiresAt = Date.parse(booking.invitationExpiresAt);
    return !Number.isNaN(expiresAt) && expiresAt <= Date.now();
  }

  protected closureLink(booking: MyBookingDto): string {
    return `/closure?booking=${encodeURIComponent(booking.referenceCode)}`;
  }

  protected isEnded(booking: MyBookingDto): boolean {
    const end = Date.parse(booking.end);
    return !Number.isNaN(end) && end <= Date.now();
  }

  protected canCancel(booking: MyBookingDto): boolean {
    return (
      booking.role === 'Organizer' &&
      booking.status === 'Confirmed' &&
      !this.isEnded(booking)
    );
  }

  protected promptCancel(booking: MyBookingDto): void {
    this.actionError.set(null);
    this.actionSuccess.set(null);
    this.confirmingCancelBookingId.set(booking.id);
  }

  protected dismissCancel(): void {
    this.confirmingCancelBookingId.set(null);
  }

  protected confirmCancel(booking: MyBookingDto): void {
    if (this.cancellingBookingId() !== null) {
      return;
    }

    this.actionError.set(null);
    this.actionSuccess.set(null);
    this.cancellingBookingId.set(booking.id);
    this.cancelSubscription?.unsubscribe();
    this.cancelSubscription = this.api.cancelBooking(booking.id).subscribe({
      next: () => {
        this.cancellingBookingId.set(null);
        this.confirmingCancelBookingId.set(null);
        this.actionSuccess.set('Prenotazione annullata. Lo spazio è di nuovo disponibile.');
        this.load();
        if (typeof window !== 'undefined') {
          window.dispatchEvent(new Event(BOOKINGS_CHANGED_EVENT));
        }
      },
      error: (error: unknown) => {
        this.cancellingBookingId.set(null);
        this.confirmingCancelBookingId.set(null);
        this.handleCancelError(error);
      },
    });
  }

  private handleCancelError(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        this.actionError.set('La sessione è scaduta. Effettua di nuovo l’accesso.');
        return;
      }

      if (error.status === 409) {
        this.actionError.set('La prenotazione risulta già annullata o non più annullabile.');
        this.load();
        return;
      }

      if (error.status === 403) {
        this.actionError.set('Solo l’organizzatore può annullare la prenotazione.');
        return;
      }

      if (error.status === 404) {
        this.actionError.set('Prenotazione non trovata.');
        this.load();
        return;
      }

      if (error.status === 503 || error.status === 0) {
        this.actionError.set('Il servizio prenotazioni non è raggiungibile. Riprova tra poco.');
        return;
      }
    }

    this.actionError.set('Non è stato possibile annullare la prenotazione. Riprova.');
  }

  protected acceptInvitation(invitation: MyInvitationDto): void {
    if (this.acceptingInvitationId() !== null) {
      return;
    }

    this.actionError.set(null);
    this.actionSuccess.set(null);
    this.acceptingInvitationId.set(invitation.id);
    this.acceptSubscription?.unsubscribe();
    this.acceptSubscription = this.api.acceptInvitation(invitation.id).subscribe({
      next: () => {
        this.acceptingInvitationId.set(null);
        this.actionSuccess.set('Invito confermato.');
        this.load();
      },
      error: (error: unknown) => {
        this.acceptingInvitationId.set(null);
        this.handleAcceptError(error);
      },
    });
  }

  private load(): void {
    this.errorMessage.set(null);
    this.invitationsUnavailable.set(false);
    this.loading.set(true);
    this.loadSubscription?.unsubscribe();

    this.loadSubscription = this.api
      .getMyBookings()
      .pipe(
        switchMap((bookings) => {
          this.bookings.set(bookings);
          if (!bookings.some((booking) => booking.hasPendingInvitation)) {
            return of([] as MyInvitationDto[]);
          }

          return this.api.getMyInvitations().pipe(
            catchError((error: unknown) => {
              // A 401 is handled by the auth interceptor (clear session and
              // redirect); any other failure only hides the confirm button.
              if (error instanceof HttpErrorResponse && error.status === 401) {
                return throwError(() => error);
              }

              this.invitationsUnavailable.set(true);
              return of([] as MyInvitationDto[]);
            }),
          );
        }),
      )
      .subscribe({
        next: (invitations) => {
          this.invitations.set(invitations);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.handleLoadError(error);
        },
      });
  }

  private handleLoadError(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        this.errorMessage.set('La sessione è scaduta. Effettua di nuovo l’accesso.');
        return;
      }

      if (error.status === 503 || error.status === 0) {
        this.errorMessage.set(
          'Il servizio prenotazioni non è al momento raggiungibile. Riprova tra poco.',
        );
        return;
      }
    }

    this.errorMessage.set('Non è stato possibile caricare le tue prenotazioni. Riprova.');
  }

  private handleAcceptError(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        this.errorMessage.set('La sessione è scaduta. Effettua di nuovo l’accesso.');
        return;
      }

      if (error.status === 409) {
        // The API uses 409 for an expired/answered invitation and for a
        // personal overlap: only the first one invalidates the invitation.
        if (this.problemTitle(error) === 'Overlapping booking') {
          this.actionError.set(
            'Hai già una prenotazione confermata che si sovrappone a questa fascia oraria.',
          );
          return;
        }

        this.actionError.set(
          'L’invito non è più disponibile: potrebbe essere scaduto o già stato risposto.',
        );
        this.load();
        return;
      }

      if (error.status === 404) {
        this.actionError.set(
          'L’invito non è più disponibile: potrebbe essere stato rimosso o la prenotazione annullata.',
        );
        this.load();
        return;
      }

      if (error.status === 403) {
        this.actionError.set('Questo invito è intestato a un altro account.');
        return;
      }

      if (error.status === 503 || error.status === 0) {
        this.actionError.set('Il servizio prenotazioni non è raggiungibile. Riprova tra poco.');
        return;
      }
    }

    this.actionError.set('Non è stato possibile confermare l’invito. Riprova.');
  }

  private problemTitle(error: HttpErrorResponse): string | null {
    const payload = error.error;
    if (!payload || typeof payload !== 'object') {
      return null;
    }

    const title = (payload as { title?: unknown }).title;
    return typeof title === 'string' ? title : null;
  }
}
