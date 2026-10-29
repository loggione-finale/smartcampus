import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { AdminNavComponent } from '../admin/admin-nav.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { SPACE_MODE_OPTIONS, SPACE_TYPE_OPTIONS, SpaceMode, SpaceType } from '../spaces/spaces.models';
import { EquipmentItem, SpaceInput, SpacesAdminApiService } from './spaces-admin-api.service';
import { describeAdminError } from './spaces-admin-error';

@Component({
  selector: 'app-space-form-page',
  imports: [
    AdminNavComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './space-form.page.html',
  styleUrl: './spaces-admin.page.scss',
})
export class SpaceFormPage implements OnInit {
  private readonly api = inject(SpacesAdminApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly spaceId = this.route.snapshot.paramMap.get('id');
  protected readonly typeOptions = SPACE_TYPE_OPTIONS;
  protected readonly modeOptions = SPACE_MODE_OPTIONS;
  protected readonly equipmentCatalog = signal<EquipmentItem[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  // Features are not editable here: an update sends back what the space already has.
  private features: Record<string, unknown> = {};

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    type: ['Classroom' as SpaceType, Validators.required],
    mode: ['Dedicated' as SpaceMode, Validators.required],
    building: ['', [Validators.required, Validators.maxLength(100)]],
    floor: [0, [Validators.required, Validators.min(-5), Validators.max(50)]],
    capacity: [1, [Validators.required, Validators.min(1), Validators.max(500)]],
    equipment: [[] as string[]],
  });

  ngOnInit(): void {
    this.api.getEquipmentCatalog().subscribe({
      next: (items) => this.equipmentCatalog.set(items),
      error: (error: HttpErrorResponse) => this.error.set(describeAdminError(error)),
    });

    if (this.spaceId) {
      this.api.getById(this.spaceId).subscribe({
        next: (space) => {
          this.features = space.features ?? {};
          this.form.patchValue(space);
        },
        error: (error: HttpErrorResponse) => this.error.set(describeAdminError(error)),
      });
    }
  }

  protected cancel(): void {
    this.router.navigate(['/admin/spaces']);
  }

  protected toggleEquipment(code: string, checked: boolean): void {
    const current = this.form.controls.equipment.value;
    this.form.controls.equipment.setValue(
      checked ? [...current, code] : current.filter((item) => item !== code),
    );
  }

  protected submit(): void {
    this.error.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const input: SpaceInput = { ...this.form.getRawValue(), features: this.features };
    const request = this.spaceId ? this.api.update(this.spaceId, input) : this.api.create(input);

    this.saving.set(true);
    request.subscribe({
      next: () => this.router.navigate(['/admin/spaces']),
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(describeAdminError(error));
      },
    });
  }
}
