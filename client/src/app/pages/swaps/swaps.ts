import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiService, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Swap } from '../../core/models';

@Component({
  selector: 'app-swaps',
  imports: [DatePipe],
  templateUrl: './swaps.html',
})
export class Swaps implements OnInit {
  private api = inject(ApiService);
  protected auth = inject(AuthService);

  swaps = signal<Swap[]>([]);
  error = signal('');
  busyId = signal<number | null>(null);

  pending = computed(() => this.swaps().filter((s) => s.status === 'Pending'));
  history = computed(() => this.swaps().filter((s) => s.status !== 'Pending'));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.swaps().subscribe({
      next: (swaps) => this.swaps.set(swaps),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  canCancel(swap: Swap): boolean {
    return swap.requestedById === this.auth.user()?.id;
  }

  act(swap: Swap, action: 'approve' | 'reject' | 'cancel'): void {
    this.error.set('');
    this.busyId.set(swap.id);

    this.api.resolveSwap(swap.id, action).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(errorMessage(err));
      },
    });
  }
}
