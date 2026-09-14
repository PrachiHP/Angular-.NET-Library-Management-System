import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { LibraryApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { Dashboard } from '../../models/models';

@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe],
  templateUrl: './dashboard.html',
})
export class DashboardPage {
  protected readonly data = signal<Dashboard | null>(null);
  protected readonly error = signal<string | null>(null);

  private readonly api = inject(LibraryApi);

  constructor() {
    this.api.dashboard().subscribe({
      next: (d) => this.data.set(d),
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
