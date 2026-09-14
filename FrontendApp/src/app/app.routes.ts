import { Routes } from '@angular/router';

import { librarianGuard, memberGuard } from './core/guards';

/**
 * Every page is lazily loaded with loadComponent, so a member never downloads
 * the librarian screens and the initial bundle stays small.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'books' },

  {
    path: 'login',
    loadComponent: () => import('./pages/auth/login').then((m) => m.Login),
    title: 'Sign in',
  },
  {
    path: 'register',
    loadComponent: () => import('./pages/auth/register').then((m) => m.Register),
    title: 'Register',
  },

  // Public: browsing the catalogue needs no account.
  {
    path: 'books',
    loadComponent: () => import('./pages/books/book-catalogue').then((m) => m.BookCatalogue),
    title: 'Catalogue',
  },
  {
    path: 'books/:id',
    loadComponent: () => import('./pages/books/book-detail').then((m) => m.BookDetailPage),
    title: 'Book',
  },

  // Member area
  {
    path: 'my/recommendations',
    canActivate: [memberGuard],
    loadComponent: () => import('./pages/member/recommendations').then((m) => m.Recommendations),
    title: 'Recommended for you',
  },
  {
    path: 'my/loans',
    canActivate: [memberGuard],
    loadComponent: () => import('./pages/member/my-loans').then((m) => m.MyLoans),
    title: 'My books',
  },

  // Librarian area. The guard is declared once on the parent and applies to
  // every child, so a new child route cannot accidentally be left unprotected.
  {
    path: 'librarian',
    canActivate: [librarianGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./pages/librarian/dashboard').then((m) => m.DashboardPage),
        title: 'Dashboard',
      },
      {
        path: 'books',
        loadComponent: () => import('./pages/librarian/manage-books').then((m) => m.ManageBooks),
        title: 'Manage books',
      },
      {
        path: 'trending',
        loadComponent: () => import('./pages/librarian/trending').then((m) => m.Trending),
        title: 'Trending',
      },
      {
        path: 'requests',
        loadComponent: () => import('./pages/librarian/requests').then((m) => m.Requests),
        title: 'Issue requests',
      },
      {
        path: 'issued',
        loadComponent: () => import('./pages/librarian/issued').then((m) => m.Issued),
        title: 'Loans',
      },
      {
        path: 'members',
        loadComponent: () => import('./pages/librarian/members').then((m) => m.MembersPage),
        title: 'Members',
      },
      {
        path: 'problems',
        loadComponent: () => import('./pages/librarian/problems').then((m) => m.Problems),
        title: 'Problems',
      },
    ],
  },

  // Unknown paths fall back rather than showing a blank page.
  { path: '**', redirectTo: 'books' },
];

