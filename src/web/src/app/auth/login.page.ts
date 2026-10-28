import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthSessionService } from './auth-session.service';
import { AuthService } from './auth.service';
import { safeReturnUrl } from './return-url';

type LoginField = 'identifier' | 'password';

@Component({
  selector: 'app-login-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    ReactiveFormsModule,
  ],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly session = inject(AuthSessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  private loginSubscription: Subscription | null = null;

  protected readonly loginForm = this.formBuilder.nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(254)]],
    password: ['', [Validators.required]],
  });

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  constructor() {
    this.destroyRef.onDestroy(() => this.loginSubscription?.unsubscribe());
  }

  ngOnInit(): void {
    this.readReason();
    if (this.session.hasValidSession()) {
      void this.router.navigateByUrl(this.destination());
    }
  }

  protected fieldError(field: LoginField): string | null {
    const control = this.loginForm.controls[field];
    if (control.hasError('required')) {
      return field === 'identifier'
        ? 'Inserisci l’email o la matricola.'
        : 'Inserisci la password.';
    }
    if (control.hasError('maxlength')) {
      return 'Puoi inserire al massimo 254 caratteri.';
    }
    return null;
  }

  protected togglePassword(): void {
    this.showPassword.update((visible) => !visible);
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    this.errorMessage.set(null);
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { identifier, password } = this.loginForm.getRawValue();

    this.loginSubscription = this.auth.login({ identifier, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.destination());
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.errorMessage.set(this.messageFor(error));
        if (error instanceof HttpErrorResponse && error.status === 401) {
          this.loginForm.controls.password.reset();
        }
      },
    });
  }

  private readReason(): void {
    const reason = this.route.snapshot.queryParamMap.get('reason');
    if (reason === 'expired') {
      this.notice.set('La sessione è scaduta. Accedi di nuovo per continuare.');
    } else if (reason === 'logout') {
      this.notice.set('Sei uscito. A presto!');
    }
  }

  private destination(): string {
    const returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
    if (returnUrl) {
      return returnUrl;
    }

    return this.session.isAdmin() ? '/admin' : '/';
  }

  private messageFor(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Il servizio ha restituito una risposta non valida. Riprova.';
    }
    if (error.status === 401) {
      return 'Email o matricola e password non corretti.';
    }
    if (error.status === 0) {
      return 'Il servizio non è raggiungibile. Controlla la connessione e riprova.';
    }
    if (error.status >= 500) {
      return 'Errore temporaneo del servizio. Riprova tra poco.';
    }
    if (error.status === 400) {
      return 'Controlla i dati inseriti e riprova.';
    }
    return 'Non è stato possibile accedere. Riprova tra poco.';
  }
}
