import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { SPACE_TYPE_OPTIONS, todayInRome, utcIsoToRomeTime } from '../spaces/spaces.models';
import { AdminApiService } from './admin-api.service';
import { AdminNavComponent } from './admin-nav.component';
import { BOOKING_STATUS_LABELS, DashboardBooking, DashboardSpace } from './admin.models';

// The API may return timestamps without the "Z" suffix: they are UTC.
const asUtc = (iso: string): string => (/(Z|[+-]\d\d:\d\d)$/.test(iso) ? iso : `${iso}Z`);

@Component({
  selector: 'app-dashboard-page',
  imports: [AdminNavComponent, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage {
  private readonly api = inject(AdminApiService);

  protected readonly typeOptions = SPACE_TYPE_OPTIONS;
  protected readonly statusLabels = BOOKING_STATUS_LABELS;
  protected readonly filters = new FormGroup({
    date: new FormControl(todayInRome(), { nonNullable: true }),
    building: new FormControl('', { nonNullable: true }),
    type: new FormControl('', { nonNullable: true }),
  });

  protected readonly spaces = signal<DashboardSpace[] | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  // One column per distinct time slot found in the day's bookings.
  protected readonly slots = computed(() => {
    const keys = new Set<string>();
    for (const space of this.spaces() ?? []) {
      for (const b of space.bookings) {
        keys.add(this.slotKey(b));
      }
    }
    return [...keys].sort().map((key) => {
      const [start, end] = key.split('|');
      return { key, label: `${utcIsoToRomeTime(asUtc(start))}–${utcIsoToRomeTime(asUtc(end))}` };
    });
  });
  protected readonly columns = computed(() => ['room', ...this.slots().map((s) => s.key)]);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getDashboard(this.filters.getRawValue()).subscribe({
      next: (spaces) => {
        this.spaces.set(spaces);
        this.loading.set(false);
      },
      error: () => {
        this.spaces.set(null);
        this.error.set('Impossibile caricare la dashboard. Riprova tra poco.');
        this.loading.set(false);
      },
    });
  }

  protected bookingsAt(space: DashboardSpace, key: string): DashboardBooking[] {
    return space.bookings.filter((b) => this.slotKey(b) === key);
  }

  protected freeSeats(space: DashboardSpace, booking: DashboardBooking): number {
    return Math.max(space.capacity - booking.occupiedSeats, 0);
  }

  private slotKey(b: DashboardBooking): string {
    return `${b.start}|${b.end}`;
  }
}
