import { Routes } from '@angular/router';
import { DashboardPage } from './dashboard.page';

export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'dashboard', component: DashboardPage, title: 'Dashboard · SmartCampus' },
  {
    path: 'alerts',
    loadComponent: () => import('./alerts.page').then((m) => m.AlertsPage),
    title: 'Avvisi · SmartCampus',
  },
  {
    path: 'report',
    loadComponent: () => import('./report.page').then((m) => m.ReportPage),
    title: 'Report · SmartCampus',
  },
];
