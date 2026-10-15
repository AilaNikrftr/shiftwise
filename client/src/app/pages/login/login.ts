import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/api.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
})
export class Login {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = '';
  password = '';
  error = signal('');
  busy = signal(false);

  submit(): void {
    this.error.set('');
    this.busy.set(true);

    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/schedule']),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.busy.set(false);
      },
    });
  }

  useDemo(email: string): void {
    this.email = email;
    this.password = 'shiftwise123';
  }
}
