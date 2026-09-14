import { environment } from '../../environments/environment';

/**
 * Turns a stored relative path ("/uploads/covers/abc.png") into a URL the
 * browser can load. Kept in one place so the origin is not hard-coded into
 * templates — in production fileOrigin is empty and the path stays relative.
 */
export function fileUrl(path: string | null | undefined): string | null {
  if (!path) return null;
  return environment.fileOrigin + path;
}
