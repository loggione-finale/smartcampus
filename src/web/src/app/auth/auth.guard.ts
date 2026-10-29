import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionService } from './auth-session.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(AuthSessionService);
  const router = inject(Router);

  const hadSession = session.hasSession();
  if (session.hasValidSession()) {
    return true;
  }

  const expired = hadSession || session.consumeExpiredNotice();
  return router.createUrlTree(['/login'], {
    queryParams: {
      returnUrl: state.url,
      ...(expired ? { reason: 'expired' } : {}),
    },
  });
};

export const adminGuard: CanActivateFn = (_route, state) => {
  const session = inject(AuthSessionService);
  const router = inject(Router);

  const hadSession = session.hasSession();
  if (!session.hasValidSession()) {
    const expired = hadSession || session.consumeExpiredNotice();
    return router.createUrlTree(['/login'], {
      queryParams: {
        returnUrl: state.url,
        ...(expired ? { reason: 'expired' } : {}),
      },
    });
  }

  if (!session.isAdmin()) {
    return router.createUrlTree(['/forbidden']);
  }

  return true;
};

// Invitation emails sent before registration had its own page link to /?returnUrl=…#registrazione.
export const legacyRegistrationGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const tree = router.parseUrl(state.url);
  if (tree.fragment !== 'registrazione') {
    return true;
  }
  return router.createUrlTree(['/register'], { queryParams: tree.queryParams });
};
