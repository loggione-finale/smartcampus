import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'spaces',
    loadChildren: () => import('./spaces/spaces.routes').then((m) => m.SPACES_ROUTES),
  },
];
