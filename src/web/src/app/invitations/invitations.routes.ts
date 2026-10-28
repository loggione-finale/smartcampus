import { Routes } from '@angular/router';
import { authGuard } from '../auth/auth.guard';

export const INVITATIONS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./invitations.page').then((m) => m.InvitationsPage),
    title: 'Inviti · SmartCampus',
  },
  {
    path: ':id',
    canActivate: [authGuard],
    loadComponent: () => import('./invitations.page').then((m) => m.InvitationsPage),
    title: 'Inviti · SmartCampus',
  },
];
