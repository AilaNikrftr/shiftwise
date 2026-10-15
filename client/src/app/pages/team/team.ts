import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ApiService, errorMessage } from '../../core/api.service';
import { User } from '../../core/models';

@Component({
  selector: 'app-team',
  imports: [FormsModule],
  templateUrl: './team.html',
})
export class Team implements OnInit {
  private api = inject(ApiService);

  people = signal<User[]>([]);
  error = signal('');
  notice = signal('');
  saving = signal(false);

  form = { fullName: '', email: '', temporaryPassword: '' };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.employees().subscribe({
      next: (people) => this.people.set(people),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  add(ngForm: NgForm): void {
    this.error.set('');
    this.notice.set('');
    this.saving.set(true);

    const { fullName, email, temporaryPassword } = this.form;
    this.api.addEmployee(fullName, email, temporaryPassword).subscribe({
      next: (user) => {
        this.saving.set(false);
        this.notice.set(
          `Added ${user.fullName}. Share their temporary password with them directly.`,
        );
        ngForm.resetForm();
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }
}
