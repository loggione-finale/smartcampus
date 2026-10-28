import { Routes } from '@angular/router';
import { staffGuard } from './admin/staff.guard';

export const routes: Routes = [
  {
    path: 'admin',
    canActivate: [staffGuard],
    loadChildren: () => import('./admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: 'spaces',
    loadChildren: () => import('./spaces/spaces.routes').then((m) => m.SPACES_ROUTES),
  },
  {
    path: 'closure',
    loadChildren: () => import('./closure/closure.routes').then((m) => m.CLOSURE_ROUTES),
  },
];
