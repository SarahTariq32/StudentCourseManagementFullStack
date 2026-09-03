export interface DecodedToken {
  role?: string;
  exp?: number;
  [key: string]: any;
}

export function decodeToken(token: string): DecodedToken | null {
  try {
    const payloadBase64 = token.split('.')[1];
    return JSON.parse(atob(payloadBase64));
  } catch {
    return null;
  }
}

export function getTokenRole(token: string): string | null {
  const decoded = decodeToken(token);
  if (!decoded) return null;
  return decoded['role'] ||
    decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
    null;
}

export function isTokenExpired(token: string): boolean {
  const decoded = decodeToken(token);
  if (!decoded || !decoded['exp']) return true;
  return Date.now() >= decoded['exp'] * 1000;
}