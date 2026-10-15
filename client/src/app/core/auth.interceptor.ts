import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;

  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      // Expired or invalid token: drop the session and go back to login.
      // Skip the login call itself so a wrong password just shows an error.
      if (err.status === 401 && token && !req.url.endsWith('/api/auth/login')) {
        auth.logout();
      }
      return throwError(() => err);
    }),
  );
};
