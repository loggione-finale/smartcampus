import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { SpacesApiService } from '../spaces/spaces-api.service';
import { bookingHref } from '../spaces/spaces.models';
import { defaultBookingSlot } from './booking-form.component';

type TeaserSpace = { id: string; label: string };

// Home preview of the booking: space, day and start, then the full form on /book.
@Component({
  selector: 'app-booking-teaser',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ReactiveFormsModule],
  templateUrl: './booking-teaser.component.html',
  styleUrl: './booking-teaser.component.scss',
})
export class BookingTeaserComponent {
  private readonly router = inject(Router);
  private readonly slot = defaultBookingSlot();

  protected readonly spaces = signal<TeaserSpace[]>([]);
  protected readonly form = new FormGroup({
    spaceId: new FormControl('', { nonNullable: true }),
    date: new FormControl(this.slot.date, { nonNullable: true, validators: [Validators.required] }),
    start: new FormControl(this.slot.start, { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    inject(SpacesApiService)
      .searchSpaces()
      .subscribe({
        next: (spaces) => {
          const active = spaces
            .filter((space) => space.isActive)
            .map((space) => ({ id: space.id, label: `${space.name} · ${space.building}` }));
          this.spaces.set(active);
          if (active.length > 0 && !this.form.controls.spaceId.value) {
            this.form.controls.spaceId.setValue(active[0].id);
          }
        },
        // The full page reports the error; here the button still leads there.
        error: () => this.spaces.set([]),
      });
  }

  protected continueToBooking(): void {
    void this.router.navigateByUrl(this.targetUrl());
  }

  // One hour from the chosen start; the booking page lets the visitor change everything.
  protected targetUrl(): string {
    const { spaceId, date, start } = this.form.getRawValue();
    const startDate = new Date(`${date}T${start}`);
    if (!spaceId) return '/book';
    if (Number.isNaN(startDate.valueOf())) return bookingHref(spaceId);
    const endDate = new Date(startDate.getTime() + 60 * 60000);
    return bookingHref(spaceId, startDate.toISOString(), endDate.toISOString());
  }
}
