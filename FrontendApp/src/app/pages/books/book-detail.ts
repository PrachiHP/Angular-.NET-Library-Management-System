import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';

import { AuthService } from '../../core/auth';
import { BookApi, EngagementApi, IssueApi, LibraryApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { fileUrl } from '../../core/file-url';
import { BookDetail, BookMemberStatus, Feedback, Quote } from '../../models/models';

@Component({
  selector: 'app-book-detail',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './book-detail.html',
  styleUrl: './book-detail.css',
})
export class BookDetailPage {
  /**
   * Bound from the :id route parameter by withComponentInputBinding(), which
   * is enabled in app.config.ts — no ActivatedRoute subscription needed.
   */
  readonly id = input.required<string>();

  protected readonly book = signal<BookDetail | null>(null);
  protected readonly status = signal<BookMemberStatus | null>(null);
  protected readonly feedback = signal<Feedback[]>([]);
  protected readonly quotes = signal<Quote[]>([]);

  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly loading = signal(true);

  // Feedback form
  protected rating = 5;
  protected comment = '';

  // Quote form
  protected quoteText = '';
  protected quoteImage: File | null = null;
  protected problemType = 'TornPage';
  protected problemDescription = '';

  protected readonly problemTypes = [
    { value: 'TornPage', label: 'Torn page' },
    { value: 'MissingPage', label: 'Missing page' },
    { value: 'WaterDamage', label: 'Water damage' },
    { value: 'BrokenSpine', label: 'Broken spine' },
    { value: 'Other', label: 'Other' },
  ];

  /// Exposed so the template can build image URLs without hard-coding a host.
  protected readonly fileUrl = fileUrl;

  protected readonly auth = inject(AuthService);
  private readonly books = inject(BookApi);
  private readonly engagement = inject(EngagementApi);
  private readonly issues = inject(IssueApi);
  private readonly library = inject(LibraryApi);

  constructor() {
    // input() values are not readable in the constructor body directly for the
    // initial load, so defer to a microtask once the input is set.
    queueMicrotask(() => this.load());
  }

  protected bookId(): number {
    return Number(this.id());
  }

  protected load(): void {
    const id = this.bookId();
    this.loading.set(true);

    this.books.get(id).subscribe({
      next: (b) => {
        this.book.set(b);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(errorText(err));
        this.loading.set(false);
      },
    });

    this.engagement.feedbackForBook(id).subscribe({
      next: (f) => this.feedback.set(f),
      error: () => this.feedback.set([]),
    });

    this.engagement.quotesForBook(id).subscribe({
      next: (q) => this.quotes.set(q),
      error: () => this.quotes.set([]),
    });

    // Only a member has a status; the endpoint is Member-only.
    if (this.auth.isMember()) {
      this.engagement.memberStatus(id).subscribe({
        next: (s) => this.status.set(s),
        error: () => this.status.set(null),
      });
    }
  }

  protected averageRating(): number | null {
    const all = this.feedback();
    if (all.length === 0) return null;
    return all.reduce((sum, f) => sum + f.rating, 0) / all.length;
  }

  protected request(): void {
    this.clearMessages();
    this.issues.request(this.bookId()).subscribe({
      next: () => {
        this.notice.set('Request submitted. A librarian will review it.');
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected reissue(): void {
    const loanId = this.status()?.issuedBookId;
    if (!loanId) return;

    this.clearMessages();
    this.issues.reissue(loanId).subscribe({
      next: (loan) => {
        this.notice.set(
          'Extended to ' + new Date(loan.dueDate).toLocaleDateString() +
            '. ' + loan.reIssuesRemaining + ' re-issue(s) remaining.',
        );
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected submitFeedback(): void {
    this.clearMessages();
    this.engagement
      .addFeedback({ bookId: this.bookId(), rating: this.rating, comment: this.comment.trim() || undefined })
      .subscribe({
        next: () => {
          this.notice.set('Thanks — your summary has been saved.');
          this.comment = '';
          this.load();
        },
        error: (err) => this.error.set(errorText(err)),
      });
  }

  protected onQuoteImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.quoteImage = input.files?.[0] ?? null;
  }

  protected submitQuote(): void {
    this.clearMessages();

    if (!this.quoteText.trim() && !this.quoteImage) {
      this.error.set('Type a quote or attach a photo of the page.');
      return;
    }

    this.engagement.addQuote(this.bookId(), this.quoteText.trim() || null, this.quoteImage).subscribe({
      next: () => {
        this.notice.set('Quote posted.');
        this.quoteText = '';
        this.quoteImage = null;
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected like(quote: Quote): void {
    this.engagement.likeQuote(quote.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected reportProblem(): void {
    this.clearMessages();
    this.library
      .reportProblem({
        bookId: this.bookId(),
        problemType: this.problemType,
        description: this.problemDescription.trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.notice.set('Reported. A librarian will look into it.');
          this.problemDescription = '';
        },
        error: (err) => this.error.set(errorText(err)),
      });
  }

  private clearMessages(): void {
    this.error.set(null);
    this.notice.set(null);
  }
}
