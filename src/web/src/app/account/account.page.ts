import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { AuthSessionService } from '../auth/auth-session.service';

@Component({
  selector: 'app-account-page',
  imports: [MatButtonModule],
  templateUrl: './account.page.html',
  styleUrl: './account.page.scss',
})
export class AccountPage {
  private readonly session = inject(AuthSessionService);
  private readonly router = inject(Router);

  protected readonly user = this.session.user;
  protected readonly isAdmin = this.session.isAdmin;
  protected readonly roleLabel = computed(() => (this.isAdmin() ? 'Amministratore' : 'Studente'));
  protected readonly memberSince = computed(() => this.formatDate(this.user()?.createdAt));

  protected logout(): void {
    this.session.clear();
    void this.router.navigate(['/login'], { queryParams: { reason: 'logout' } });
  }

  private formatDate(value: string | undefined): string {
    if (!value) {
      return '—';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }

    return new Intl.DateTimeFormat('it-IT', { dateStyle: 'long' }).format(date);
  }
}
