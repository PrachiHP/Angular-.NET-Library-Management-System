import { HttpErrorResponse } from '@angular/common/http';

import { ApiError } from '../models/models';

/**
 * Turns any HttpErrorResponse into one readable sentence. Every API failure
 * uses the same ErrorResponse shape, so this is the only place that needs to
 * know it — components just call errorText(err).
 */
export function errorText(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Cannot reach the API. Is the backend running on port 5041?';
    }

    const body = error.error as ApiError | null;

    if (body?.errors) {
      // Field errors: show them rather than the generic summary.
      const first = Object.values(body.errors).flat()[0];
      if (first) return first;
    }

    if (body?.message) return body.message;

    return `Request failed (HTTP ${error.status}).`;
  }

  return 'Something went wrong.';
}
