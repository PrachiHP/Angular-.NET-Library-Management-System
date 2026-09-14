import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { BookApi, EngagementApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { fileUrl } from '../../core/file-url';
import { Book, BookDetail } from '../../models/models';

interface BookForm {
  title: string;
  isbn: string;
  description: string;
  publishedYear: number | null;
  categoryId: number;
  totalCopies: number;
}

function emptyForm(): BookForm {
  return { title: '', isbn: '', description: '', publishedYear: null, categoryId: 1, totalCopies: 1 };
}

@Component({
  selector: 'app-manage-books',
  imports: [FormsModule, RouterLink],
  templateUrl: './manage-books.html',
  styleUrl: './manage-books.css',
})
export class ManageBooks {
  protected readonly books = signal<Book[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly saving = signal(false);

  /** null = the form is creating; a number = editing that book. */
  protected readonly editingId = signal<number | null>(null);
  protected readonly formOpen = signal(false);

  protected form: BookForm = emptyForm();
  protected search = '';

  /**
   * Seeded categories from AppDbContext.Seed.cs. Hard-coded because there is no
   * categories endpoint yet — the obvious next step is GET /api/categories.
   */
  protected readonly categories = [
    { id: 1, name: 'Fiction' },
    { id: 2, name: 'Non-Fiction' },
    { id: 3, name: 'Science & Technology' },
    { id: 4, name: 'History' },
    { id: 5, name: 'Children' },
  ];

  protected readonly fileUrl = fileUrl;

  private readonly api = inject(BookApi);
  private readonly engagement = inject(EngagementApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.list({ search: this.search.trim() || undefined }).subscribe({
      next: (b) => this.books.set(b),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected startCreate(): void {
    this.form = emptyForm();
    this.editingId.set(null);
    this.formOpen.set(true);
    this.clearMessages();
  }

  protected startEdit(book: Book): void {
    this.clearMessages();

    // The list DTO lacks description and publishedYear, so fetch the detail.
    this.api.get(book.id).subscribe({
      next: (b: BookDetail) => {
        this.form = {
          title: b.title,
          isbn: b.isbn,
          description: b.description ?? '',
          publishedYear: b.publishedYear,
          categoryId: b.categoryId,
          totalCopies: b.totalCopies,
        };
        this.editingId.set(b.id);
        this.formOpen.set(true);
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected cancel(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
    this.clearMessages();
  }

  protected save(): void {
    this.clearMessages();
    this.saving.set(true);

    const payload = {
      title: this.form.title.trim(),
      isbn: this.form.isbn.trim(),
      description: this.form.description.trim() || null,
      publishedYear: this.form.publishedYear,
      categoryId: Number(this.form.categoryId),
      totalCopies: Number(this.form.totalCopies),
      authorIds: [] as number[],
    };

    const id = this.editingId();
    const request = id === null ? this.api.create(payload) : this.api.update(id, payload);

    request.subscribe({
      next: (book) => {
        this.saving.set(false);
        this.notice.set(id === null ? 'Book added.' : 'Book updated.');
        this.formOpen.set(false);
        this.editingId.set(null);
        this.load();
        void book;
      },
      // 422 for a duplicate ISBN, or for reducing copies below the number
      // currently on loan.
      error: (err) => {
        this.saving.set(false);
        this.error.set(errorText(err));
      },
    });
  }

  protected onCover(event: Event, book: Book): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.clearMessages();
    this.engagement.uploadCover(book.id, file).subscribe({
      next: () => {
        this.notice.set('Cover updated for "' + book.title + '".');
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });

    // Allow re-selecting the same file after a failure.
    input.value = '';
  }

  protected remove(book: Book): void {
    if (!confirm('Remove "' + book.title + '" from the catalogue?')) return;

    this.clearMessages();
    this.api.remove(book.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  private clearMessages(): void {
    this.error.set(null);
    this.notice.set(null);
  }
}
