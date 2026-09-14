import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth';

/**
 * Attaches the bearer token to every outgoing request, so no service has to
 * remember to do it, and signs the user out on a 401.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  // HttpRequest is IMMUTABLE — you cannot assign to req.headers. clone() with
  // modifications is the only way to change an outgoing request.
  const request = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401 means the token is missing, expired or invalid — the session is
      // over. 403 means the token is fine but the role is wrong, so do NOT log
      // out: the user is legitimately signed in, just not permitted.
      if (error.status === 401 && auth.isLoggedIn()) {
        auth.logout();
      }
      return throwError(() => error);
    }),
  );
};
