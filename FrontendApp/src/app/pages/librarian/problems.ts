import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { LibraryApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { BookProblem } from '../../models/models';

@Component({
  selector: 'app-problems',
  imports: [DatePipe],
  templateUrl: './problems.html',
})
export class Problems {
  protected readonly problems = signal<BookProblem[]>([]);
  protected readonly error = signal<string | null>(null);
  protected unresolvedOnly = true;

  private readonly api = inject(LibraryApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.problems(this.unresolvedOnly || undefined).subscribe({
      next: (p) => this.problems.set(p),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected toggle(): void {
    this.unresolvedOnly = !this.unresolvedOnly;
    this.load();
  }

  protected resolve(problem: BookProblem): void {
    this.api.resolveProblem(problem.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
