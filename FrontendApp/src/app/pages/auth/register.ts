import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth';
import { errorText } from '../../core/error-text';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
})
export class Register {
  protected model = { email: '', password: '', firstName: '', lastName: '', phone: '' };
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected submit(): void {
    this.busy.set(true);
    this.error.set(null);

    this.auth.register(this.model).subscribe({
      next: () => {
        this.busy.set(false);
        this.router.navigateByUrl('/books');
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(errorText(err));
      },
    });
  }
}
