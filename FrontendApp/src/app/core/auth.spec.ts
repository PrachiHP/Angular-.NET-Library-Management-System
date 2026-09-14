import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';

import { AuthService } from './auth';
import { environment } from '../../environments/environment';
import { AuthResponse } from '../models/models';

function makeResponse(overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    token: 'fake.jwt.token',
    // An hour from now, so the token is valid unless a test says otherwise.
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    role: 'Member',
    name: 'Priya Sharma',
    email: 'priya@example.com',
    memberId: 1,
    ...overrides,
  };
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('starts signed out', () => {
    expect(service.isLoggedIn()).toBeFalse();
    expect(service.role()).toBeNull();
  });

  it('stores the session after a successful login', () => {
    service.login('priya@example.com', 'Member#1234').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(makeResponse());

    expect(service.isLoggedIn()).toBeTrue();
    expect(service.isMember()).toBeTrue();
    expect(service.isLibrarian()).toBeFalse();
    expect(service.displayName()).toBe('Priya Sharma');
  });

  it('reports the librarian role correctly', () => {
    service.login('librarian@library.local', 'Librarian#123').subscribe();
    httpMock
      .expectOne(`${environment.apiUrl}/auth/login`)
      .flush(makeResponse({ role: 'Librarian', memberId: null, name: 'Librarian' }));

    expect(service.isLibrarian()).toBeTrue();
    expect(service.isMember()).toBeFalse();
  });

  it('clears the session on logout', () => {
    service.login('priya@example.com', 'x').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/auth/login`).flush(makeResponse());

    service.logout();

    expect(service.isLoggedIn()).toBeFalse();
    expect(service.token()).toBeNull();
  });

  it('refuses an expired token and signs out', () => {
    service.login('priya@example.com', 'x').subscribe();
    httpMock
      .expectOne(`${environment.apiUrl}/auth/login`)
      // Already expired.
      .flush(makeResponse({ expiresAt: new Date(Date.now() - 1000).toISOString() }));

    expect(service.token()).toBeNull();
    expect(service.isLoggedIn()).toBeFalse();
  });
});
