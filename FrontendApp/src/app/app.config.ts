import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideClientHydration(withEventReplay()),
    // No zone.js in this project — change detection is driven by signals.
    provideZonelessChangeDetection(),
    // withFetch: required under SSR, where XMLHttpRequest does not exist.
    // withInterceptors: attaches the JWT to every outgoing request.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
  ],
};
