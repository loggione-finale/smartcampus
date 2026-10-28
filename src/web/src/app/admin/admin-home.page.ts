import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { AuthSessionService } from '../auth/auth-session.service';

@Component({
  selector: 'app-admin-home-page',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './admin-home.page.html',
  styleUrl: './admin-home.page.scss',
})
export class AdminHomePage {
  private readonly session = inject(AuthSessionService);
  private readonly router = inject(Router);

  protected readonly user = this.session.user;

  protected logout(): void {
    this.session.clear();
    void this.router.navigate(['/login'], { queryParams: { reason: 'logout' } });
  }
}
