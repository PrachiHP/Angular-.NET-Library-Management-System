import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { IssueApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { IssueRequest } from '../../models/models';

@Component({
  selector: 'app-requests',
  imports: [DatePipe],
  templateUrl: './requests.html',
})
export class Requests {
  protected readonly requests = signal<IssueRequest[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected showAll = false;

  private readonly api = inject(IssueApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.allRequests(this.showAll ? undefined : 'Pending').subscribe({
      next: (r) => this.requests.set(r),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected toggle(): void {
    this.showAll = !this.showAll;
    this.load();
  }

  protected approve(req: IssueRequest): void {
    this.error.set(null);
    this.api.approve(req.id).subscribe({
      next: () => {
        this.notice.set(`Approved "${req.bookTitle}" for ${req.memberName}.`);
        this.load();
      },
      // 422 when no copies are free — the API checks availability at approval
      // time, not request time.
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected reject(req: IssueRequest): void {
    const reason = prompt(`Reject "${req.bookTitle}" — reason?`);
    if (!reason) return;

    this.api.reject(req.id, reason).subscribe({
      next: () => {
        this.notice.set('Request rejected.');
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
