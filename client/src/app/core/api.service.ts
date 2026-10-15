import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HoursRow, SaveShift, Shift, Swap, User } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  // Employees
  employees(): Observable<User[]> {
    return this.http.get<User[]>('/api/employees');
  }

  addEmployee(fullName: string, email: string, temporaryPassword: string): Observable<User> {
    return this.http.post<User>('/api/employees', { fullName, email, temporaryPassword });
  }

  // Shifts
  shifts(from: string, to: string): Observable<Shift[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<Shift[]>('/api/shifts', { params });
  }

  createShift(shift: SaveShift): Observable<Shift> {
    return this.http.post<Shift>('/api/shifts', shift);
  }

  updateShift(id: number, shift: SaveShift): Observable<Shift> {
    return this.http.put<Shift>(`/api/shifts/${id}`, shift);
  }

  deleteShift(id: number): Observable<void> {
    return this.http.delete<void>(`/api/shifts/${id}`);
  }

  // Swaps
  swaps(): Observable<Swap[]> {
    return this.http.get<Swap[]>('/api/swaps');
  }

  requestSwap(shiftId: number, targetEmployeeId: number, note: string | null): Observable<Swap> {
    return this.http.post<Swap>('/api/swaps', { shiftId, targetEmployeeId, note });
  }

  resolveSwap(id: number, action: 'approve' | 'reject' | 'cancel'): Observable<Swap> {
    return this.http.post<Swap>(`/api/swaps/${id}/${action}`, {});
  }

  // Reports
  hours(from: string, to: string): Observable<HoursRow[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<HoursRow[]>('/api/reports/hours', { params });
  }

  // Fetched as a blob rather than a plain link because the request needs the auth header.
  hoursCsv(from: string, to: string): Observable<Blob> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get('/api/reports/hours/export', { params, responseType: 'blob' });
  }
  
  hoursXlsx(from: string, to: string): Observable<Blob> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get('/api/reports/hours/export.xlsx', { params, responseType: 'blob' });
  }

}

// The API returns ProblemDetails with the message in "title".
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return "Can't reach the server. Is the API running?";
    }
    const title = err.error?.title;
    if (typeof title === 'string' && title.length > 0) {
      return title;
    }
    return `Request failed (${err.status}).`;
  }
  return 'Something went wrong.';
}
