import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { SPACE_TYPE_LABELS, SpaceType, todayInRome } from '../spaces/spaces.models';
import { AdminApiService } from './admin-api.service';
import { AdminNavComponent } from './admin-nav.component';
import { GROUP_BY_OPTIONS, StatsGroup, StatsGroupBy } from './admin.models';

const DAY_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: 'app-report-page',
  imports: [AdminNavComponent, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule],
  templateUrl: './report.page.html',
  styleUrl: './report.page.scss',
})
export class ReportPage {
  private readonly api = inject(AdminApiService);

  protected readonly groupByOptions = GROUP_BY_OPTIONS;
  protected readonly columns = [
    'label',
    'totalBookings',
    'confirmed',
    'cancelled',
    'closed',
    'spaceUsed',
    'spaceNotUsed',
    'withoutComment',
    'totalAttendees',
    'issuesReported',
  ];
  protected readonly filters = new FormGroup({
    from: new FormControl(todayInRome(new Date(Date.now() - 6 * DAY_MS)), { nonNullable: true }),
    to: new FormControl(todayInRome(), { nonNullable: true }),
    groupBy: new FormControl<StatsGroupBy>('Space', { nonNullable: true }),
  });

  protected readonly rows = signal<StatsGroup[] | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    const { from, to, groupBy } = this.filters.getRawValue();
    if (!from || !to || from > to) {
      this.rows.set(null);
      this.error.set('Controlla le date: "Dal" non può essere dopo "Al".');
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.getStats({ from, to, groupBy }).subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.rows.set(null);
        this.error.set('Impossibile caricare il report. Riprova tra poco.');
        this.loading.set(false);
      },
    });
  }

  protected label(row: StatsGroup): string {
    const typeLabel = SPACE_TYPE_LABELS[row.label as SpaceType];
    return this.filters.controls.groupBy.value === 'Type' && typeLabel ? typeLabel : row.label;
  }
}
