import { HttpErrorResponse, HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth';

let isRefreshing = false;
const refreshedTokenSubject = new BehaviorSubject<string | null>(null);

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  const isAuthEndpoint =
    req.url.includes('/Auth/login') ||
    req.url.includes('/Auth/register') ||
    req.url.includes('/Auth/refresh');

  const authorizedReq = token && !isAuthEndpoint
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthEndpoint) {
        return handle401(req, next, authService, router);
      }
      return throwError(() => error);
    })
  );
};

function handle401(
  req: HttpRequest<any>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
) {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshedTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap(res => {
        isRefreshing = false;
        refreshedTokenSubject.next(res.token);
        const retried = req.clone({ setHeaders: { Authorization: `Bearer ${res.token}` } });
        return next(retried);
      }),
      catchError(err => {
        isRefreshing = false;
        authService.logout();
        router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
        return throwError(() => err);
      })
    );
  }

  // A refresh is already in flight — queue this request until it resolves
  return refreshedTokenSubject.pipe(
    filter(token => token !== null),
    take(1),
    switchMap(token => {
      const retried = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
      return next(retried);
    })
  );
}