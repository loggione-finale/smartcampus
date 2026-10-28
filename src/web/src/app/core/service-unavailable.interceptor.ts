import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TimeoutError, catchError, throwError, timeout } from 'rxjs';
import { isHealthCheck, serviceUnavailableMessage } from './service-unavailable.messages';
import { ServiceUnavailableNotifier } from './service-unavailable.notifier';

/**
 * A request that waits longer than this is treated as a service that does not
 * answer. The backend already times out cross-service calls after 3 seconds.
 */
export const SERVICE_UNAVAILABLE_TIMEOUT_MS = 10_000;

const SERVICE_UNAVAILABLE_STATUSES = new Set([0, 503, 504]);

/**
 * Central place for "service unavailable" feedback: a snackbar for `503`,
 * status `0` (offline or connection refused) and timeouts, while the error is
 * still propagated so each page can keep its own state and retry button.
 */
export const serviceUnavailableInterceptor: HttpInterceptorFn = (request, next) => {
  if (isHealthCheck(request.url)) {
    return next(request);
  }

  const notifier = inject(ServiceUnavailableNotifier);

  return next(request).pipe(
    timeout(SERVICE_UNAVAILABLE_TIMEOUT_MS),
    catchError((error: unknown) => {
      if (error instanceof TimeoutError) {
        const timedOut = new HttpErrorResponse({
          status: 504,
          statusText: 'Gateway Timeout',
          url: request.url,
          error: { detail: 'The service did not answer in time.' },
        });
        notifier.notify(serviceUnavailableMessage(request.url, timedOut.status));
        return throwError(() => timedOut);
      }

      if (error instanceof HttpErrorResponse && SERVICE_UNAVAILABLE_STATUSES.has(error.status)) {
        notifier.notify(serviceUnavailableMessage(request.url, error.status));
      }

      return throwError(() => error);
    }),
  );
};
