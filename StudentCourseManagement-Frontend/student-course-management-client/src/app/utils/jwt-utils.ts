// export interface DecodedToken {
//   role?: string;
//   exp?: number;
//   [key: string]: any;
// }

// export function decodeToken(token: string): DecodedToken | null {
//   try {
//     const payloadBase64 = token.split('.')[1];
//     return JSON.parse(atob(payloadBase64));
//   } catch {
//     return null;
//   }
// }

// export function getTokenRole(token: string): string | null {
//   const decoded = decodeToken(token);
//   if (!decoded) return null;
//   return decoded['role'] ||
//     decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
//     null;
// }

// export function isTokenExpired(token: string): boolean {
//   const decoded = decodeToken(token);
//   if (!decoded || !decoded['exp']) return true;
//   return Date.now() >= decoded['exp'] * 1000;
// }

export interface DecodedToken {
  role?: string;
  exp?: number;
  [key: string]: any;
}

export function decodeToken(token: string): DecodedToken | null {
  try {
    const base64Url = token.split('.')[1];
    if (!base64Url) return null;
    
    // Replace URL-safe base64 characters
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    
    // Safely handle UTF-8 encoded strings
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );

    return JSON.parse(jsonPayload);
  } catch {
    return null;
  }
}

export function getTokenRole(token: string): string | null {
  const decoded = decodeToken(token);
  if (!decoded) return null;
  return (
    decoded['role'] ||
    decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
    null
  );
}

export function isTokenExpired(token: string): boolean {
  const decoded = decodeToken(token);
  if (!decoded || !decoded['exp']) return true;
  return Date.now() >= decoded['exp'] * 1000;
}