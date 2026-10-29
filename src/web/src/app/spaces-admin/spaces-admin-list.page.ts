import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { AdminNavComponent } from '../admin/admin-nav.component';
import { Router } from '@angular/router';
import { SPACE_TYPE_LABELS, SpaceDto, formatFloorLabel } from '../spaces/spaces.models';
import { SpacesAdminApiService } from './spaces-admin-api.service';
import { describeAdminError } from './spaces-admin-error';

@Component({
  selector: 'app-spaces-admin-list-page',
  imports: [AdminNavComponent, MatButtonModule],
  templateUrl: './spaces-admin-list.page.html',
  styleUrl: './spaces-admin.page.scss',
})
export class SpacesAdminListPage implements OnInit {
  private readonly api = inject(SpacesAdminApiService);
  private readonly router = inject(Router);

  protected readonly spaces = signal<SpaceDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly typeLabels = SPACE_TYPE_LABELS;
  protected readonly formatFloorLabel = formatFloorLabel;

  ngOnInit(): void {
    this.api.listAll().subscribe({
      next: (spaces) => {
        this.spaces.set(spaces);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(describeAdminError(error));
        this.loading.set(false);
      },
    });
  }

  protected go(path: string): void {
    this.router.navigate(['/admin/spaces', ...path.split('/')]);
  }

  protected toggleActive(space: SpaceDto): void {
    this.error.set(null);
    this.api.setActive(space.id, !space.isActive).subscribe({
      next: (updated) =>
        this.spaces.update((list) => list.map((s) => (s.id === updated.id ? updated : s))),
      error: (error: HttpErrorResponse) => this.error.set(describeAdminError(error)),
    });
  }
}
