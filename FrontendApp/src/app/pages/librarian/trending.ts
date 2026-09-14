import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { TrendingApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { fileUrl } from '../../core/file-url';
import { TrendDirection, TrendingResponse } from '../../models/models';

@Component({
  selector: 'app-trending',
  imports: [DatePipe, RouterLink],
  templateUrl: './trending.html',
  styleUrl: './trending.css',
})
export class Trending {
  protected readonly data = signal<TrendingResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly windows = [7, 30, 90];
  protected readonly selectedDays = signal(30);

  protected readonly fileUrl = fileUrl;

  private readonly api = inject(TrendingApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.get(this.selectedDays()).subscribe({
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

  protected selectWindow(days: number): void {
    this.selectedDays.set(days);
    this.load();
  }

  /** Arrow for the trend badge. Kept here so the template stays declarative. */
  protected arrow(trend: TrendDirection): string {
    switch (trend) {
      case 'Rising':
        return '▲';
      case 'Falling':
        return '▼';
      case 'New':
        return '★';
      default:
        return '—';
    }
  }

  /**
   * Percent change, or a dash when there is nothing to compare against.
   * The API returns null rather than 0 or 100 for "no previous activity",
   * because growth from zero is not a percentage.
   */
  protected percent(value: number | null): string {
    if (value === null) return '—';
    return (value > 0 ? '+' : '') + value + '%';
  }

  protected overallChange(): number | null {
    const d = this.data();
    if (!d || d.totalIssuesInPreviousWindow === 0) return null;
    return Math.round(
      ((d.totalIssuesInWindow - d.totalIssuesInPreviousWindow) / d.totalIssuesInPreviousWindow) * 100,
    );
  }
}
