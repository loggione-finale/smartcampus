import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './auth/auth.guard';

export const routes: Routes = [
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
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./admin/admin-home.page').then((m) => m.AdminHomePage),
    title: 'Pannello amministratore · SmartCampus',
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
];
