import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { getTokenRole, isTokenExpired } from '../utils/jwt-utils';

export const roleGuard = (expectedRole: string): CanActivateFn => {
  return () => {
    const router = inject(Router);
    const token = localStorage.getItem('token');

    if (!token || isTokenExpired(token)) {
      router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
      return false;
    }

    const userRole = getTokenRole(token);
    if (userRole && userRole.toLowerCase() === expectedRole.toLowerCase()) {
      return true;
    }

    router.navigate(['/login']);
    return false;
  };
};