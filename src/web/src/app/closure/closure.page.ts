import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ClosureApiService } from './closure-api.service';
import { PendingBookingItem, SubmitClosingCommentPayload } from './closure.models';

@Component({
  selector: 'app-closure-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './closure.page.html',
  styleUrl: './closure.page.scss',
})
export class ClosurePage implements OnInit {
  private readonly api = inject(ClosureApiService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  readonly pendingBookings = signal<PendingBookingItem[]>([]);
  readonly selectedBooking = signal<PendingBookingItem | null>(null);
  readonly loading = signal<boolean>(true);
  readonly submitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly form: FormGroup = this.fb.group({
    spaceUsed: [true, [Validators.required]],
    attendeesCount: [1, [Validators.required, Validators.min(0)]],
    issuesReported: [false],
    comment: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
  });

  ngOnInit(): void {
    this.form.get('spaceUsed')?.valueChanges.subscribe((used) => {
      if (!used) {
        this.form.patchValue({ attendeesCount: 0 }, { emitEvent: false });
      }
    });

    this.loadPendingBookings();
  }

  loadPendingBookings(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.api.getPendingBookings().subscribe({
      next: (bookings) => {
        this.pendingBookings.set(bookings);
        this.loading.set(false);

        const targetCode = this.route.snapshot.queryParamMap.get('booking');
        if (targetCode) {
          const match = bookings.find((b) => b.referenceCode === targetCode);
          if (match) {
            this.selectBooking(match);
            return;
          }
        }

        if (bookings.length > 0 && !this.selectedBooking()) {
          this.selectBooking(bookings[0]);
        }
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        if (error.status === 401) {
          this.errorMessage.set("Devi effettuare l'accesso per verificare le prenotazioni da commentare.");
        } else {
          this.errorMessage.set('Impossibile caricare le prenotazioni in sospeso. Riprova più tardi.');
        }
      },
    });
  }

  selectBooking(booking: PendingBookingItem): void {
    this.selectedBooking.set(booking);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.form.reset({
      spaceUsed: true,
      attendeesCount: 1,
      issuesReported: false,
      comment: '',
    });
  }

  onSubmit(): void {
    if (this.form.invalid || !this.selectedBooking()) {
      this.form.markAllAsTouched();
      return;
    }

    const booking = this.selectedBooking()!;
    const payload: SubmitClosingCommentPayload = {
      spaceUsed: Boolean(this.form.value.spaceUsed),
      attendeesCount: Number(this.form.value.attendeesCount),
      issuesReported: Boolean(this.form.value.issuesReported),
      comment: String(this.form.value.comment).trim(),
    };

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.api.submitComment(booking.referenceCode, payload).subscribe({
      next: () => {
        this.submitting.set(false);
        this.successMessage.set(
          `Commento finale inviato con successo per la prenotazione ${booking.referenceCode}! Ora puoi effettuare nuove prenotazioni.`,
        );

        const remaining = this.pendingBookings().filter(
          (b) => b.referenceCode !== booking.referenceCode,
        );
        this.pendingBookings.set(remaining);

        if (remaining.length > 0) {
          this.selectBooking(remaining[0]);
        } else {
          this.selectedBooking.set(null);
          this.form.reset();
        }
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(this.formatErrorMessage(error));
      },
    });
  }

  get commentLength(): number {
    return this.form.get('comment')?.value?.length ?? 0;
  }

  private formatErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 409) {
      return (
        error.error?.detail ||
        error.error?.title ||
        'La prenotazione ha già un commento finale registrato o non è ancora conclusa.'
      );
    }
    if (error.status === 404) {
      return 'Prenotazione non trovata o non associata al tuo account.';
    }
    if (error.status === 401) {
      return "Sessione scaduta. Devi effettuare nuovamente l'accesso.";
    }
    if (error.status === 400) {
      if (error.error?.errors) {
        const firstKey = Object.keys(error.error.errors)[0];
        const messages = error.error.errors[firstKey];
        if (Array.isArray(messages) && messages.length > 0) {
          return `${firstKey}: ${messages[0]}`;
        }
      }
      return error.error?.detail || error.error?.title || 'Dati del commento non validi.';
    }
    return 'Si è verificato un errore durante il salvataggio del commento. Riprova più tardi.';
  }
}
