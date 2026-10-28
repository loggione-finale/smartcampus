import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

// shortcut: the role is a plain signal until the login (#82) exists; then fill it from the session.
@Injectable({ providedIn: 'root' })
export class StaffAccess {
  readonly role = signal<string | null>(null);

  isAdmin(): boolean {
    return this.role() === 'Admin';
  }
}

export const adminGuard: CanActivateFn = () => {
  if (inject(StaffAccess).isAdmin()) {
    return true;
  }
  return inject(Router).parseUrl('/');
};
