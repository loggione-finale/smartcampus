import { Routes } from '@angular/router';
import { adminGuard, authGuard, legacyRegistrationGuard } from './auth/auth.guard';

export const routes: Routes = [
  // The home itself is drawn by App; this route only forwards old registration links.
  { path: '', pathMatch: 'full', canActivate: [legacyRegistrationGuard], children: [] },
  {
    path: 'register',
    loadComponent: () => import('./auth/register.page').then((m) => m.RegisterPage),
    title: 'Registrati · SmartCampus',
  },
  {
    path: 'login',
    loadComponent: () => import('./auth/login.page').then((m) => m.LoginPage),
    title: 'Accedi · SmartCampus',
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('./account/account.page').then((m) => m.AccountPage),
    title: 'Area personale · SmartCampus',
  },
  {
    path: 'bookings',
    canActivate: [authGuard],
    loadComponent: () => import('./bookings/my-bookings.page').then((m) => m.MyBookingsPage),
    title: 'Le mie prenotazioni · SmartCampus',
  },
  {
    path: 'admin/spaces',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('./spaces-admin/spaces-admin.routes').then((m) => m.SPACES_ADMIN_ROUTES),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadChildren: () => import('./admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./forbidden/forbidden.page').then((m) => m.ForbiddenPage),
    title: 'Accesso negato · SmartCampus',
  },
  {
    path: 'spaces',
    loadChildren: () => import('./spaces/spaces.routes').then((m) => m.SPACES_ROUTES),
  },
  {
    path: 'closure',
    loadChildren: () => import('./closure/closure.routes').then((m) => m.CLOSURE_ROUTES),
  },
  {
    path: 'invitations',
    loadChildren: () =>
      import('./invitations/invitations.routes').then((m) => m.INVITATIONS_ROUTES),
  },
];
