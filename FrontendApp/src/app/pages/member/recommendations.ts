import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { IssueApi, RecommendationApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { fileUrl } from '../../core/file-url';
import { Recommendation, RecommendationResponse } from '../../models/models';

@Component({
  selector: 'app-recommendations',
  imports: [RouterLink],
  templateUrl: './recommendations.html',
  styleUrl: './recommendations.css',
})
export class Recommendations {
  protected readonly data = signal<RecommendationResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  /** Whether to reveal the raw affinity scores. Off by default. */
  protected readonly showScores = signal(false);

  protected readonly fileUrl = fileUrl;

  private readonly api = inject(RecommendationApi);
  private readonly issues = inject(IssueApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.forMe().subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(errorText(err));
        this.loading.set(false);
      },
    });
  }

  protected toggleScores(): void {
    this.showScores.update((v) => !v);
  }

  protected request(rec: Recommendation): void {
    this.error.set(null);
    this.notice.set(null);

    this.issues.request(rec.bookId).subscribe({
      next: () => {
        this.notice.set('Requested "' + rec.title + '". A librarian will review it.');
        // Reload so the requested book drops out of the list — the engine
        // excludes books with a pending request.
        this.load();
      },
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
