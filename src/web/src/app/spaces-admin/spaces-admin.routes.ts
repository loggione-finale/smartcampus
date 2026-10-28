import { Routes } from '@angular/router';
import { SpaceFormPage } from './space-form.page';
import { SpacesAdminListPage } from './spaces-admin-list.page';
import { adminGuard } from './staff-access';

export const SPACES_ADMIN_ROUTES: Routes = [
  {
    path: '',
    canActivate: [adminGuard],
    children: [
      { path: '', component: SpacesAdminListPage, title: 'Gestione spazi · SmartCampus' },
      { path: 'new', component: SpaceFormPage, title: 'Nuovo spazio · SmartCampus' },
      { path: ':id/edit', component: SpaceFormPage, title: 'Modifica spazio · SmartCampus' },
    ],
  },
];
