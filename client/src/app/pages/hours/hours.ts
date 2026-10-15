import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, errorMessage } from '../../core/api.service';
import { addDays, startOfWeek, toDateInput } from '../../core/dates';
import { HoursRow } from '../../core/models';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-hours',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './hours.html',
})
export class Hours implements OnInit {
  private api = inject(ApiService);

  // "to" is inclusive in the UI; the API takes an exclusive end, so we add a day before sending.
  from = toDateInput(startOfWeek(new Date()));
  to = toDateInput(addDays(startOfWeek(new Date()), 6));

  rows = signal<HoursRow[]>([]);
  error = signal('');

  total = computed(() => this.rows().reduce((sum, r) => sum + r.totalHours, 0));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.error.set('');
    const [from, to] = this.range();

    this.api.hours(from, to).subscribe({
      next: (rows) => this.rows.set(rows),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  exportCsv(): void {
    const [from, to] = this.range();
    this.download(this.api.hoursCsv(from, to), 'csv');
  }

  exportXlsx(): void {
    const [from, to] = this.range();
    this.download(this.api.hoursXlsx(from, to), 'xlsx');
  }

  private download(request: Observable<Blob>, extension: string): void {
    request.subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `hours_${this.from}_to_${this.to}.${extension}`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private range(): [string, string] {
    const endExclusive = addDays(new Date(`${this.to}T00:00:00`), 1);
    return [`${this.from}T00:00:00`, `${toDateInput(endExclusive)}T00:00:00`];
  }
}
