import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { IssueApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { IssuedBook } from '../../models/models';

@Component({
  selector: 'app-issued',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './issued.html',
})
export class Issued {
  protected readonly loans = signal<IssuedBook[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected overdueOnly = false;

  private readonly api = inject(IssueApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    const source = this.overdueOnly ? this.api.overdue() : this.api.allLoans();
    source.subscribe({
      next: (loans) => this.loans.set(loans),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected toggle(): void {
    this.overdueOnly = !this.overdueOnly;
    this.load();
  }

  /**
   * Whole days until the loan is due; negative once overdue. Date-only so a
   * book due later today reads as 1 day rather than 0.
   */
  protected daysUntilDue(loan: IssuedBook): number {
    const due = new Date(loan.dueDate);
    due.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.round((due.getTime() - today.getTime()) / 86400000);
  }

  protected returnBook(loan: IssuedBook): void {
    this.error.set(null);
    this.api.return_(loan.id).subscribe({
      next: (updated) => {
        this.notice.set(
          updated.fine > 0
            ? 'Returned "' + updated.bookTitle + '". Fine due: ' + updated.fine +
              ' (' + updated.overdueDays + ' days overdue).'
            : 'Returned "' + updated.bookTitle + '". No fine.',
        );
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
