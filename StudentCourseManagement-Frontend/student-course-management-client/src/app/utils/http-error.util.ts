import { HttpErrorResponse } from '@angular/common/http';

export function extractErrorMessage(
  err: HttpErrorResponse,
  fallback: string = 'Something went wrong. Please try again.'
): string {
  if (!err) return fallback;

  const body = err.error;

  if (typeof body === 'string' && body.trim().length > 0) {
    return body;
  }

  if (body?.message) {
    return body.message;
  }

  if (body?.title) {
    return body.title;
  }

  // ASP.NET ModelState validation errors: { errors: { field: ["msg"] } }
  if (body?.errors) {
    const firstField = Object.keys(body.errors)[0];
    const firstMsg = firstField && body.errors[firstField]?.[0];
    if (firstMsg) return firstMsg;
  }

  if (err.status === 0) {
    return 'Unable to reach the server. Please check your connection.';
  }

  return fallback;
}