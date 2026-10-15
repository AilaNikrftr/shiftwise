export type UserRole = 'Manager' | 'Employee';
export type SwapStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthResponse {
  token: string;
  user: User;
}

// Dates come from the API as local ISO strings without a time zone, e.g. "2026-10-15T09:00:00".
export interface Shift {
  id: number;
  employeeId: number;
  employeeName: string;
  startsAt: string;
  endsAt: string;
  position: string;
  notes: string | null;
}

export interface SaveShift {
  employeeId: number;
  startsAt: string;
  endsAt: string;
  position: string;
  notes: string | null;
}

export interface Swap {
  id: number;
  shiftId: number;
  shiftStartsAt: string;
  shiftEndsAt: string;
  position: string;
  requestedById: number;
  requestedByName: string;
  targetEmployeeId: number;
  targetEmployeeName: string;
  status: SwapStatus;
  note: string | null;
  createdAt: string;
}

export interface HoursRow {
  employeeId: number;
  employeeName: string;
  shiftCount: number;
  totalHours: number;
}
