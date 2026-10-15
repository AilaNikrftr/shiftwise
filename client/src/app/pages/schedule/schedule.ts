import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, forkJoin } from 'rxjs';
import { ApiService, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import {
  addDays,
  combine,
  hoursBetween,
  sameDay,
  startOfWeek,
  toDateInput,
  toLocalIso,
  toTimeInput,
} from '../../core/dates';
import { SaveShift, Shift, User } from '../../core/models';

type Panel = { kind: 'create' } | { kind: 'edit'; shift: Shift } | { kind: 'swap'; shift: Shift };

@Component({
  selector: 'app-schedule',
  imports: [FormsModule, DatePipe, DecimalPipe],
  templateUrl: './schedule.html',
  styleUrl: './schedule.css',
})
export class Schedule implements OnInit {
  private api = inject(ApiService);
  protected auth = inject(AuthService);

  weekStart = signal(startOfWeek(new Date()));
  employees = signal<User[]>([]);
  shifts = signal<Shift[]>([]);
  loading = signal(false);
  error = signal('');
  notice = signal('');

  panel = signal<Panel | null>(null);
  saving = signal(false);
  panelError = signal('');

  // Shift form
  form = { employeeId: 0, date: '', start: '09:00', end: '17:00', position: '', notes: '' };
  // Swap form
  swapForm = { targetEmployeeId: 0, note: '' };

  days = computed(() => Array.from({ length: 7 }, (_, i) => addDays(this.weekStart(), i)));

  // Managers schedule themselves sometimes, so they stay in the grid too.
  rows = computed(() => {
    const shifts = this.shifts();
    return this.employees().map((employee) => {
      const mine = shifts.filter((s) => s.employeeId === employee.id);
      return {
        employee,
        cells: this.days().map((day) => mine.filter((s) => sameDay(new Date(s.startsAt), day))),
        hours: mine.reduce((sum, s) => sum + hoursBetween(s.startsAt, s.endsAt), 0),
      };
    });
  });

  coworkers = computed(() => this.employees().filter((e) => e.id !== this.auth.user()?.id));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const from = toLocalIso(this.weekStart());
    const to = toLocalIso(addDays(this.weekStart(), 7));

    this.loading.set(true);
    this.error.set('');

    forkJoin({ employees: this.api.employees(), shifts: this.api.shifts(from, to) }).subscribe({
      next: ({ employees, shifts }) => {
        this.employees.set(employees);
        this.shifts.set(shifts);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  moveWeek(weeks: number): void {
    this.weekStart.set(addDays(this.weekStart(), weeks * 7));
    this.closePanel();
    this.load();
  }

  thisWeek(): void {
    this.weekStart.set(startOfWeek(new Date()));
    this.closePanel();
    this.load();
  }

  isToday(day: Date): boolean {
    return sameDay(day, new Date());
  }

  isMine(shift: Shift): boolean {
    return shift.employeeId === this.auth.user()?.id;
  }

  // ---- Panel handling ----

  openCreate(employee: User, day: Date): void {
    if (!this.auth.isManager()) return;
    this.form = {
      employeeId: employee.id,
      date: toDateInput(day),
      start: '09:00',
      end: '17:00',
      position: '',
      notes: '',
    };
    this.openPanel({ kind: 'create' });
  }

  openShift(shift: Shift): void {
    if (this.auth.isManager()) {
      const start = new Date(shift.startsAt);
      this.form = {
        employeeId: shift.employeeId,
        date: toDateInput(start),
        start: toTimeInput(start),
        end: toTimeInput(new Date(shift.endsAt)),
        position: shift.position,
        notes: shift.notes ?? '',
      };
      this.openPanel({ kind: 'edit', shift });
      return;
    }

    if (this.isMine(shift) && new Date(shift.startsAt) > new Date()) {
      this.swapForm = { targetEmployeeId: this.coworkers()[0]?.id ?? 0, note: '' };
      this.openPanel({ kind: 'swap', shift });
    }
  }

  closePanel(): void {
    this.panel.set(null);
    this.panelError.set('');
  }

  private openPanel(panel: Panel): void {
    this.notice.set('');
    this.panelError.set('');
    this.panel.set(panel);
  }

  // ---- Actions ----

  saveShift(): void {
    const panel = this.panel();
    if (!panel || panel.kind === 'swap') return;

    const body: SaveShift = {
      employeeId: Number(this.form.employeeId),
      startsAt: combine(this.form.date, this.form.start),
      endsAt: this.endTimestamp(),
      position: this.form.position,
      notes: this.form.notes || null,
    };

    const request =
      panel.kind === 'edit'
        ? this.api.updateShift(panel.shift.id, body)
        : this.api.createShift(body);

    this.run(request, panel.kind === 'edit' ? 'Shift updated.' : 'Shift added.');
  }

  deleteShift(): void {
    const panel = this.panel();
    if (panel?.kind !== 'edit') return;
    if (!confirm(`Delete ${panel.shift.employeeName}'s ${panel.shift.position} shift?`)) return;

    this.run(this.api.deleteShift(panel.shift.id), 'Shift deleted.');
  }

  requestSwap(): void {
    const panel = this.panel();
    if (panel?.kind !== 'swap') return;

    this.run(
      this.api.requestSwap(
        panel.shift.id,
        Number(this.swapForm.targetEmployeeId),
        this.swapForm.note || null,
      ),
      'Swap requested. Your manager has to approve it.',
    );
  }

  // An end time earlier than the start means the shift runs past midnight.
  private endTimestamp(): string {
    const endDate =
      this.form.end <= this.form.start
        ? toDateInput(addDays(new Date(`${this.form.date}T00:00:00`), 1))
        : this.form.date;
    return combine(endDate, this.form.end);
  }

  private run(request: Observable<unknown>, message: string): void {
    this.saving.set(true);
    this.panelError.set('');

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.closePanel();
        this.notice.set(message);
        this.load();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.panelError.set(errorMessage(err));
      },
    });
  }
}
