import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { SPACE_TYPE_OPTIONS, todayInRome, utcIsoToRomeTime } from '../spaces/spaces.models';
import { AdminApiService } from './admin-api.service';
import { CELL_STATUS_LABELS, DashboardCell, DashboardDto } from './admin.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage {
  private readonly api = inject(AdminApiService);

  protected readonly typeOptions = SPACE_TYPE_OPTIONS;
  protected readonly filters = new FormGroup({
    date: new FormControl(todayInRome(), { nonNullable: true }),
    building: new FormControl('', { nonNullable: true }),
    type: new FormControl('', { nonNullable: true }),
  });

  protected readonly data = signal<DashboardDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly slots = computed(() =>
    (this.data()?.rooms[0]?.cells ?? []).map((cell) => ({
      key: cell.start,
      label: `${utcIsoToRomeTime(cell.start)}–${utcIsoToRomeTime(cell.end)}`,
    })),
  );
  protected readonly columns = computed(() => ['room', ...this.slots().map((s) => s.key)]);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getDashboard(this.filters.getRawValue()).subscribe({
      next: (dto) => {
        this.data.set(dto);
        this.loading.set(false);
      },
      error: () => {
        this.data.set(null);
        this.error.set('Impossibile caricare la dashboard. Riprova tra poco.');
        this.loading.set(false);
      },
    });
  }

  protected cell(cells: DashboardCell[], key: string): DashboardCell | undefined {
    return cells.find((c) => c.start === key);
  }

  protected cellText(cell: DashboardCell | undefined): string {
    if (!cell) {
      return '–';
    }
    const label = CELL_STATUS_LABELS[cell.status];
    return cell.status === 'Partial' ? `${label}: ${cell.freeSeats}` : label;
  }
}
