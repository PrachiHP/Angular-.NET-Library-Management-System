import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth';
import { errorText } from '../../core/error-text';

interface DemoAccount {
  role: string;
  email: string;
  password: string;
}

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  protected email = '';
  protected password = '';
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  /**
   * Mirrors the "Seed" section of BackendAPI/appsettings.json, which
   * DataSeeder creates on first run. If those values change, change these.
   */
  protected readonly demoAccounts: DemoAccount[] = [
    { role: 'Librarian', email: 'librarian@library.local', password: 'Librarian#123' },
    { role: 'Member', email: 'member@library.local', password: 'Member#123' },
  ];

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Fills the form so a reviewer does not have to retype the credentials. */
  protected useDemo(account: DemoAccount): void {
    this.email = account.email;
    this.password = account.password;
    this.error.set(null);
  }

  protected submit(): void {
    this.busy.set(true);
    this.error.set(null);

    this.auth.login(this.email.trim(), this.password).subscribe({
      next: (res) => {
        this.busy.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const fallback = res.role === 'Librarian' ? '/librarian/dashboard' : '/books';
        this.router.navigateByUrl(returnUrl ?? fallback);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(errorText(err));
      },
    });
  }
}
