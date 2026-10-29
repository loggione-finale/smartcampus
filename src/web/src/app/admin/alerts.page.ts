import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from './admin-api.service';
import { AdminNavComponent } from './admin-nav.component';
import { ALERT_KIND_LABELS, AdminAlert, AlertKind, AlertStatus } from './admin.models';

@Component({
  selector: 'app-alerts-page',
  imports: [AdminNavComponent, DatePipe, FormsModule],
  templateUrl: './alerts.page.html',
  styleUrl: './alerts.page.scss',
})
export class AlertsPage {
  private readonly api = inject(AdminApiService);

  protected readonly kindLabels = ALERT_KIND_LABELS;
  protected readonly kinds = Object.keys(ALERT_KIND_LABELS) as AlertKind[];

  protected statusFilter: AlertStatus | '' = 'Open';
  protected readonly kindFilter = signal<AlertKind | ''>('');

  protected readonly alerts = signal<AdminAlert[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly feedback = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);

  // Text typed in the resolve note / cancel reason fields, by alert id.
  protected readonly notes: Record<string, string | undefined> = {};
  protected readonly reasons: Record<string, string | undefined> = {};

  // The API filters only by status, so the kind is filtered here.
  protected readonly visible = computed(() =>
    this.alerts().filter((a) => !this.kindFilter() || a.kind === this.kindFilter()),
  );

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getAlerts(this.statusFilter || undefined).subscribe({
      next: (list) => {
        this.alerts.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.alerts.set([]);
        this.error.set('Impossibile caricare gli avvisi. Riprova tra poco.');
        this.loading.set(false);
      },
    });
  }

  protected resolve(alert: AdminAlert): void {
    this.busyId.set(alert.id);
    this.feedback.set(null);
    this.api.resolveAlert(alert.id, this.notes[alert.id] ?? '').subscribe({
      next: () => {
        this.busyId.set(null);
        this.feedback.set('Avviso segnato come gestito.');
        this.load();
      },
      error: (e: HttpErrorResponse) =>
        this.fail(e, 'Avviso già gestito o non trovato.', 'Impossibile risolvere l’avviso.'),
    });
  }

  protected cancelBooking(alert: AdminAlert): void {
    const code = alert.bookingReferenceCode;
    const reason = (this.reasons[alert.id] ?? '').trim();
    if (!code || !reason) {
      return;
    }
    this.busyId.set(alert.id);
    this.feedback.set(null);
    this.api.cancelBooking(code, reason).subscribe({
      next: () => {
        this.busyId.set(null);
        this.feedback.set(`Prenotazione ${code} annullata. I partecipanti ricevono un’email.`);
        this.reasons[alert.id] = '';
      },
      error: (e: HttpErrorResponse) =>
        this.fail(
          e,
          'Prenotazione già annullata, già conclusa o non trovata.',
          'Impossibile annullare la prenotazione.',
        ),
    });
  }

  private fail(e: HttpErrorResponse, conflictMessage: string, genericMessage: string): void {
    this.busyId.set(null);
    this.feedback.set(e.status === 404 || e.status === 409 ? conflictMessage : genericMessage);
  }
}
