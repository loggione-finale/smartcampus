import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { safeReturnUrl } from './return-url';

type RegistrationResult = 'created' | null;

type RegisterRequest = {
  matricola: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  password: string;
};

type RegistrationField = keyof RegisterRequest;

@Component({
  selector: 'app-register-page',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  templateUrl: './register.page.html',
  styleUrl: './register.page.scss',
})
export class RegisterPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // Set by the registration link of an invitation email: back to the invitation once registered.
  protected readonly registrationReturnUrl = signal<string | null>(
    inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl'),
  );
  protected readonly loginHref = computed(() => {
    const returnUrl = safeReturnUrl(this.registrationReturnUrl());
    return returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login';
  });
  protected readonly registrationResult = signal<RegistrationResult>(null);
  protected readonly registrationSubmitting = signal(false);
  protected readonly registrationError = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly registrationForm = this.formBuilder.nonNullable.group({
    matricola: ['', [Validators.required, Validators.pattern(/^\d{10}$/)]],
    firstName: [
      '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(50), Validators.pattern(/^[\p{L}][\p{L} '-]*$/u)],
    ],
    lastName: [
      '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(50), Validators.pattern(/^[\p{L}][\p{L} '-]*$/u)],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(/^\+39\d{9,10}$/)]],
    password: ['', [Validators.required, Validators.minLength(10), Validators.pattern(/^(?=.*[\p{L}])(?=.*\d).+$/u)]],
  });

  protected fieldError(field: RegistrationField): string | null {
    const control = this.registrationForm.controls[field];
    if (control.hasError('required')) return 'Campo obbligatorio.';
    if (control.hasError('minlength')) return `Inserisci almeno ${control.getError('minlength').requiredLength} caratteri.`;
    if (control.hasError('maxlength')) return `Puoi inserire al massimo ${control.getError('maxlength').requiredLength} caratteri.`;
    if (control.hasError('email')) return 'Inserisci un indirizzo email valido.';
    if (control.hasError('pattern')) {
      const messages: Partial<Record<RegistrationField, string>> = {
        matricola: 'La matricola ha 10 cifre.',
        firstName: 'Usa lettere, spazi, apostrofi o trattini.',
        lastName: 'Usa lettere, spazi, apostrofi o trattini.',
        phoneNumber: 'Usa il formato italiano, ad esempio +393331234567.',
        password: 'La password deve contenere almeno una lettera e una cifra.',
      };
      return messages[field] ?? 'Formato non valido.';
    }
    return this.serverErrors()[field]?.[0] ?? null;
  }

  protected submitRegistration(): void {
    this.registrationError.set(null);
    this.clearServerErrors();

    if (this.registrationForm.invalid) {
      this.registrationForm.markAllAsTouched();
      return;
    }

    this.registrationSubmitting.set(true);
    const request: RegisterRequest = this.registrationForm.getRawValue();

    this.http.post('/api/users/auth/register', request).subscribe({
      next: () => {
        this.registrationSubmitting.set(false);
        // Users arriving from the registration link in an invitation email return
        // to the invitation after signing up (the /invitations route is protected:
        // if not logged in yet, they pass through login with returnUrl).
        // safeReturnUrl rejects external paths (//host, backslash, /login).
        const returnUrl = safeReturnUrl(this.registrationReturnUrl());
        if (returnUrl) {
          void this.router.navigateByUrl(returnUrl);
          return;
        }
        this.registrationResult.set('created');
      },
      error: (error: HttpErrorResponse) => this.handleRegistrationError(error),
    });
  }

  protected resetRegistration(): void {
    this.registrationForm.reset();
    this.registrationResult.set(null);
    this.registrationError.set(null);
    this.serverErrors.set({});
  }

  private handleRegistrationError(error: HttpErrorResponse): void {
    this.registrationSubmitting.set(false);
    const payload = error.error as { errors?: Record<string, string[]>; detail?: string } | null;

    if (error.status === 400 && payload?.errors) {
      const errors = Object.entries(payload.errors).reduce<Record<string, string[]>>((mapped, [field, messages]) => {
        const normalizedField = field.charAt(0).toLowerCase() + field.slice(1);
        mapped[normalizedField] = messages;
        return mapped;
      }, {});
      this.setServerErrors(errors);
      this.registrationError.set('Controlla i campi evidenziati.');
      return;
    }

    if (error.status === 409) {
      const detail = payload?.detail ?? 'I dati inseriti risultano già registrati.';
      const field = detail.toLowerCase().includes('matricola') ? 'matricola' : 'email';
      this.setServerErrors({ [field]: [detail] });
      this.registrationError.set('Esiste già un account con uno dei dati inseriti.');
      return;
    }

    this.registrationError.set('Non è stato possibile completare la registrazione. Riprova tra poco.');
  }

  private clearServerErrors(): void {
    this.serverErrors.set({});
    for (const control of Object.values(this.registrationForm.controls)) {
      if (!control.hasError('server')) continue;
      const errors = { ...control.errors };
      delete errors['server'];
      control.setErrors(Object.keys(errors).length > 0 ? errors : null);
    }
  }

  private setServerErrors(errors: Record<string, string[]>): void {
    this.serverErrors.set(errors);
    for (const [field, messages] of Object.entries(errors)) {
      const control = this.registrationForm.get(field);
      if (control) {
        control.setErrors({ ...control.errors, server: messages[0] });
        control.markAsTouched();
      }
    }
  }
}
