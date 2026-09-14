import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: '**',
    // Client, not Prerender. Prerender generates static HTML at BUILD time,
    // which is wrong for live catalogue data and impossible for authenticated
    // pages — there is no logged-in user at build time, and localStorage (where
    // the JWT lives) does not exist in Node.
    renderMode: RenderMode.Client,
  },
];
