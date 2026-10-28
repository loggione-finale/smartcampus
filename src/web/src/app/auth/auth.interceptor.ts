import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthSessionService } from './auth-session.service';

const AUTH_ENDPOINTS = [
  /\/api\/users\/auth\/login(?:[/?#]|$)/,
  /\/api\/users\/auth\/register(?:[/?#]|$)/,
];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(AuthSessionService);
  const router = inject(Router);

  const isApiRequest = /^\/api(?:[/?#]|$)/.test(request.url);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((pattern) => pattern.test(request.url));
  const shouldAuthenticate = isApiRequest && !isAuthEndpoint;
  const hadSession = shouldAuthenticate && session.hasSession();
  const token = shouldAuthenticate ? session.validToken() : null;
  const authenticatedRequest = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        hadSession &&
        session.hasSession()
      ) {
        const currentPath = router.url.split(/[?#]/, 1)[0];
        const shouldRedirect = currentPath !== '/login';
        session.clear();

        if (shouldRedirect) {
          void router.navigate(['/login'], {
            queryParams: { returnUrl: router.url, reason: 'expired' },
          });
        }
      }

      return throwError(() => error);
    }),
  );
};
