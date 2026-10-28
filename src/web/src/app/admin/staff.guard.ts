import { CanActivateFn } from '@angular/router';
import { currentRole } from './admin-session';

export const staffGuard: CanActivateFn = () => currentRole() === 'Admin';
