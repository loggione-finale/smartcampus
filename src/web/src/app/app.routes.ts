import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'spaces',
    loadChildren: () => import('./spaces/spaces.routes').then((m) => m.SPACES_ROUTES),
  },
  {
    path: 'closure',
    loadChildren: () => import('./closure/closure.routes').then((m) => m.CLOSURE_ROUTES),
  },
];
