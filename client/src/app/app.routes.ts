import { Routes } from '@angular/router';
import { authGuard, guestGuard, managerGuard } from './core/guards';
import { Hours } from './pages/hours/hours';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Schedule } from './pages/schedule/schedule';
import { Swaps } from './pages/swaps/swaps';
import { Team } from './pages/team/team';

export const routes: Routes = [
  { path: 'login', component: Login, canActivate: [guestGuard], title: 'Sign in · ShiftWise' },
  {
    path: 'register',
    component: Register,
    canActivate: [guestGuard],
    title: 'Get started · ShiftWise',
  },
  {
    path: 'schedule',
    component: Schedule,
    canActivate: [authGuard],
    title: 'Schedule · ShiftWise',
  },
  { path: 'swaps', component: Swaps, canActivate: [authGuard], title: 'Swaps · ShiftWise' },
  {
    path: 'team',
    component: Team,
    canActivate: [authGuard, managerGuard],
    title: 'Team · ShiftWise',
  },
  {
    path: 'hours',
    component: Hours,
    canActivate: [authGuard, managerGuard],
    title: 'Hours · ShiftWise',
  },
  { path: '', pathMatch: 'full', redirectTo: 'schedule' },
  { path: '**', redirectTo: 'schedule' },
];
