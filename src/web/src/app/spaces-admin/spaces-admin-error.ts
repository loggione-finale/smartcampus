import { HttpErrorResponse } from '@angular/common/http';

export function describeAdminError(error: HttpErrorResponse): string {
  if (error.status === 401 || error.status === 403) {
    return 'Non hai i permessi per gestire gli spazi.';
  }
  if (error.status === 404) {
    return 'Spazio non trovato.';
  }
  if (error.status === 400 && typeof error.error?.detail === 'string') {
    return error.error.detail;
  }
  return 'Operazione non riuscita. Riprova tra poco.';
}
