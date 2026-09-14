import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth';

/** Requires any signed-in user. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isLoggedIn()) return true;

  // Remember where they were headed so login can send them back.
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Requires the Librarian role. */
export const librarianGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  // Signed in but wrong role: send them somewhere they can actually use rather
  // than back to a login form they have already completed.
  return auth.isLibrarian() ? true : router.createUrlTree(['/books']);
};

/** Requires the Member role. */
export const memberGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  return auth.isMember() ? true : router.createUrlTree(['/librarian/dashboard']);
};
