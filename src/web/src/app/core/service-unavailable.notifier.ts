import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** A repeated identical message inside this window is not shown twice. */
export const SERVICE_UNAVAILABLE_DEDUPE_MS = 5_000;

/** Snackbar shown by `serviceUnavailableInterceptor` when a service does not answer. */
@Injectable({ providedIn: 'root' })
export class ServiceUnavailableNotifier {
  private readonly snackBar = inject(MatSnackBar);
  private lastMessage: string | null = null;
  private lastShownAt = 0;

  notify(message: string): void {
    const now = Date.now();
    const repeated =
      message === this.lastMessage && now - this.lastShownAt < SERVICE_UNAVAILABLE_DEDUPE_MS;
    if (repeated) {
      return;
    }

    this.lastMessage = message;
    this.lastShownAt = now;
    this.snackBar.open(message, 'Chiudi', {
      duration: 6_000,
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
      panelClass: 'sc-snackbar',
      politeness: 'assertive',
    });
  }
}
