// import { TestBed } from '@angular/core/testing';
// import { HttpInterceptorFn } from '@angular/common/http';

// import { authInterceptor } from './auth-interceptor';

// describe('authInterceptor', () => {
//   const interceptor: HttpInterceptorFn = (req, next) =>
//     TestBed.runInInjectionContext(() => authInterceptor(req, next));

//   beforeEach(() => {
//     TestBed.configureTestingModule({});
//   });

//   it('should be created', () => {
//     expect(interceptor).toBeTruthy();
//   });
// });
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

  // Case-insensitive check to prevent interception of Auth calls
  const urlLower = req.url.toLowerCase();
  const isAuthEndpoint =
    urlLower.includes('/auth/login') ||
    urlLower.includes('/auth/register') ||
    urlLower.includes('/auth/refresh');

  const authorizedReq = token && !isAuthEndpoint
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if ((error.status === 401 || error.status === 403) && !isAuthEndpoint) {
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
        if (res && res.token) {
          refreshedTokenSubject.next(res.token);
          const retried = req.clone({ setHeaders: { Authorization: `Bearer ${res.token}` } });
          return next(retried);
        } else {
          authService.logout();
          router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
          return throwError(() => new Error('Invalid refresh token response'));
        }
      }),
      catchError(err => {
        isRefreshing = false;
        authService.logout();
        router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
        return throwError(() => err);
      })
    );
  }

  return refreshedTokenSubject.pipe(
    filter(token => token !== null),
    take(1),
    switchMap(token => {
      const retried = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
      return next(retried);
    })
  );
}