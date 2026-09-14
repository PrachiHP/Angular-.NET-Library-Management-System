import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MemberApi } from '../../core/api';
import { errorText } from '../../core/error-text';
import { Member } from '../../models/models';

@Component({
  selector: 'app-members',
  imports: [DatePipe, FormsModule],
  templateUrl: './members.html',
})
export class MembersPage {
  protected readonly members = signal<Member[]>([]);
  protected readonly error = signal<string | null>(null);
  protected search = '';

  private readonly api = inject(MemberApi);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.list({ search: this.search.trim() || undefined }).subscribe({
      next: (m) => this.members.set(m),
      error: (err) => this.error.set(errorText(err)),
    });
  }

  protected deactivate(member: Member): void {
    if (!confirm('Deactivate ' + member.fullName + '?')) return;

    this.error.set(null);
    this.api.deactivate(member.id).subscribe({
      next: () => this.load(),
      // 422 if they still hold books — the API refuses, and says how many.
      error: (err) => this.error.set(errorText(err)),
    });
  }
}
