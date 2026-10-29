import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute } from '@angular/router';
import {
  AcceptInvitationError,
  InvitationsApiService,
} from './invitations-api.service';
import {
  INVITATION_STATUS_LABELS,
  Invitation,
  InvitationStatus,
  formatInvitationDate,
  formatInvitationDuration,
  formatInvitationParticipants,
  formatInvitationTimeRange,
} from './invitations.models';

const ACCEPT_ERROR_MESSAGES: Record<AcceptInvitationError, string> = {
  'not-found': 'Invito non trovato.',
  expired: 'Invito scaduto: sei stato escluso dalla prenotazione.',
  conflict: 'Hai già una prenotazione che si sovrappone a questo orario.',
  unknown: 'Si è verificato un errore durante la conferma. Riprova più tardi.',
};

/** Terminal states after which an invitation is no longer confirmable: the card must update. */
const NOT_CONFIRMABLE_ERRORS: readonly AcceptInvitationError[] = ['expired', 'not-found'];

@Component({
  selector: 'app-invitations-page',
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  templateUrl: './invitations.page.html',
  styleUrl: './invitations.page.scss',
})
export class InvitationsPage implements OnInit {
  private readonly api = inject(InvitationsApiService);
  private readonly route = inject(ActivatedRoute);

  protected readonly invitations = signal<Invitation[]>([]);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly acceptingId = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly statusLabels = INVITATION_STATUS_LABELS;

  protected readonly highlightedId = signal<string | null>(null);

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('id');
    this.highlightedId.set(routeId);
    this.loadInvitations();
  }

  protected statusLabel(status: InvitationStatus): string {
    return INVITATION_STATUS_LABELS[status] ?? status;
  }

  protected formatDeadline(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    return new Intl.DateTimeFormat('it-IT', {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Europe/Rome',
    }).format(date);
  }

  protected formatInvitationDate(value: string): string {
    return formatInvitationDate(value);
  }

  protected formatInvitationTimeRange(start: string, end: string): string {
    return formatInvitationTimeRange(start, end);
  }

  protected formatInvitationDuration(start: string, end: string): string {
    return formatInvitationDuration(start, end);
  }

  protected formatInvitationParticipants(count: number): string {
    return formatInvitationParticipants(count);
  }

  protected confirm(invitation: Invitation): void {
    this.acceptingId.set(invitation.id);
    this.successMessage.set(null);
    this.errorMessage.set(null);

    this.api.accept(invitation.id).subscribe({
      next: (updated) => {
        this.acceptingId.set(null);
        this.invitations.update((list) =>
          list.map((item) => (item.id === updated.id ? updated : item)),
        );
        this.successMessage.set('Partecipazione confermata.');
      },
      error: (error: AcceptInvitationError) => {
        this.acceptingId.set(null);
        this.errorMessage.set(ACCEPT_ERROR_MESSAGES[error] ?? ACCEPT_ERROR_MESSAGES.unknown);
        // Expired or non-existent: the invitation can no longer be confirmed;
        // the card displays its actual status instead of an active button.
        if (NOT_CONFIRMABLE_ERRORS.includes(error)) {
          const newStatus: InvitationStatus =
            error === 'expired' ? 'Expired' : 'Cancelled';
          this.invitations.update((list) =>
            list.map((item) =>
              item.id === invitation.id ? { ...item, status: newStatus } : item,
            ),
          );
        }
      },
    });
  }

  private loadInvitations(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.api.getMine().subscribe({
      next: (invitations) => {
        this.invitations.set(invitations);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(
          "Impossibile caricare gli inviti. Riprova più tardi.",
        );
      },
    });
  }
}
