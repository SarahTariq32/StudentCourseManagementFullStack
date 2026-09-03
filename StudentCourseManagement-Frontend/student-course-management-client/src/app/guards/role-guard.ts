import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';

export const roleGuard = (expectedRole: string): CanActivateFn => {
  return () => {
    const router = inject(Router);
    const token = localStorage.getItem('token');

    if (!token) {
      router.navigate(['/login']);
      return false;
    }

    try {
      const payloadBase64 = token.split('.')[1];
      const decodedPayload = JSON.parse(atob(payloadBase64));
      
      const userRole = decodedPayload['role'] || 
                       decodedPayload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

      if (userRole && userRole.toLowerCase() === expectedRole.toLowerCase()) {
        return true;
      }
    } catch (e) {
      console.error('Invalid token payload:', e);
    }

    router.navigate(['/login']);
    return false;
  };
};