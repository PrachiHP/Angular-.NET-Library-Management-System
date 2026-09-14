import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { IssueApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { IssueRequest, IssuedBook } from '../../models/models';

@Component({
  selector: 'app-my-loans',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './my-loans.html',
})
export class MyLoans {
  protected readonly loans = signal<IssuedBook[]>([]);
  protected readonly requests = signal<IssueRequest[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  private readonly api = inject(IssueApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.myLoans().subscribe({
      next: (loans) => this.loans.set(loans),
      error: (err) => this.error.set(errorText(err)),
    });

    this.api.myRequests().subscribe({
      next: (requests) => this.requests.set(requests),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected reissue(loan: IssuedBook): void {
    this.error.set(null);
    this.notice.set(null);

    this.api.reissue(loan.id).subscribe({
      next: (updated) =>
        this.notice.set(
          `Extended "${updated.bookTitle}" to ${new Date(updated.dueDate).toLocaleDateString()}. ` +
            `${updated.reIssuesRemaining} re-issue(s) remaining.`,
        ),
      error: (err) => this.error.set(errorText(err)),
    });

    this.load();
  }
}
