import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth';
import { BookApi, IssueApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { Book } from '../../models/models';

@Component({
  selector: 'app-book-catalogue',
  imports: [FormsModule, RouterLink],
  templateUrl: './book-catalogue.html',
})
export class BookCatalogue {
  protected readonly books = signal<Book[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected search = '';
  protected onlyAvailable = false;

  protected readonly auth = inject(AuthService);
  private readonly api = inject(BookApi);
  private readonly issues = inject(IssueApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api
      .list({ search: this.search.trim() || undefined, onlyAvailable: this.onlyAvailable || undefined })
      .subscribe({
        next: (books) => {
          this.books.set(books);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(errorText(err));
          this.loading.set(false);
        },
      });
  }

  protected request(book: Book): void {
    this.notice.set(null);
    this.error.set(null);

    this.issues.request(book.id).subscribe({
      next: () => this.notice.set(`Requested "${book.title}". A librarian will review it.`),
      // The API enforces the holding / pending / cooldown rules and returns a
      // readable 422; surface that message rather than inventing our own.
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected remove(book: Book): void {
    if (!confirm(`Remove "${book.title}" from the catalogue?`)) return;

    this.api.remove(book.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
