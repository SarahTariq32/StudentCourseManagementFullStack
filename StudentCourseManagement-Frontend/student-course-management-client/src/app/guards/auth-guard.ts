import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { isTokenExpired } from '../utils/jwt-utils';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  const token = localStorage.getItem('token');

  if (token && !isTokenExpired(token)) {
    return true;
  }

  router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
  return false;
};