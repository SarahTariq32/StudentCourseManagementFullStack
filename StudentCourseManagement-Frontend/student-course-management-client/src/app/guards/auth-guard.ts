import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const token = localStorage.getItem('token');

  // If a valid JWT token exists, grant access
  if (token) {
    return true;
  }

  // Otherwise, redirect unauthorized users back to login
  router.navigate(['/login']);
  return false;
};