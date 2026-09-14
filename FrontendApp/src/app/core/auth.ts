import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { environment } from '../../environments/environment';
import { AuthResponse, UserRole } from '../models/models';

const STORAGE_KEY = 'library.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // The session as a signal, so guards, the nav bar and any component all react
  // to login/logout without a manual subscription.
  private readonly session = signal<AuthResponse | null>(this.restore());

  readonly currentUser = this.session.asReadonly();
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly role = computed<UserRole | null>(() => this.session()?.role ?? null);
  readonly isLibrarian = computed(() => this.role() === 'Librarian');
  readonly isMember = computed(() => this.role() === 'Member');
  readonly displayName = computed(() => this.session()?.name ?? '');

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap((res) => this.persist(res)));
  }

  register(payload: {
    email: string;
    password: string;
    firstName: string;
    lastName: string;
    phone?: string;
    address?: string;
  }): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, payload)
      .pipe(tap((res) => this.persist(res)));
  }

  logout(): void {
    this.session.set(null);
    this.safeRemove();
    this.router.navigate(['/login']);
  }

  token(): string | null {
    const session = this.session();
    if (!session) return null;

    // A token past its expiry is worse than none — it guarantees a 401 on every
    // request. Drop it and force a fresh login.
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      this.logout();
      return null;
    }

    return session.token;
  }

  private persist(res: AuthResponse): void {
    this.session.set(res);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(res));
    } catch {
      // Private browsing or blocked storage. The session still works for this
      // tab; it just will not survive a reload.
    }
  }

  private restore(): AuthResponse | null {
    // Every localStorage access is guarded: it throws in some privacy modes,
    // and does not exist at all during server-side rendering.
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;

      const parsed = JSON.parse(raw) as AuthResponse;
      if (new Date(parsed.expiresAt).getTime() <= Date.now()) {
        this.safeRemove();
        return null;
      }
      return parsed;
    } catch {
      return null;
    }
  }

  private safeRemove(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignored */
    }
  }
}
