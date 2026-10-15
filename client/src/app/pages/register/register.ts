import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/api.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
})
export class Register {
  private auth = inject(AuthService);
  private router = inject(Router);

  companyName = '';
  fullName = '';
  email = '';
  password = '';
  error = signal('');
  busy = signal(false);

  submit(): void {
    this.error.set('');
    this.busy.set(true);

    this.auth.register(this.companyName, this.fullName, this.email, this.password).subscribe({
      next: () => this.router.navigate(['/team']),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.busy.set(false);
      },
    });
  }
}
