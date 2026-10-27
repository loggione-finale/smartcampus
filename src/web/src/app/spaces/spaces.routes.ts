import { Routes } from '@angular/router';
import { SpaceAvailabilityPage } from './space-availability.page';
import { SpacesCatalogPage } from './spaces-catalog.page';

export const SPACES_ROUTES: Routes = [
  {
    path: '',
    component: SpacesCatalogPage,
    title: 'Catalogo spazi · SmartCampus',
  },
  {
    path: ':id',
    component: SpaceAvailabilityPage,
    title: 'Disponibilità spazio · SmartCampus',
  },
];
