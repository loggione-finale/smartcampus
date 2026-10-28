import { UserRole } from './auth.models';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length < 2 || !parts[1]) {
    return null;
  }

  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
    const bytes = Uint8Array.from(atob(padded), (character) => character.charCodeAt(0));
    const parsed: unknown = JSON.parse(new TextDecoder().decode(bytes));
    return parsed !== null && typeof parsed === 'object'
      ? (parsed as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

export function roleFromToken(token: string | null): UserRole | null {
  if (!token) {
    return null;
  }

  const payload = decodeJwtPayload(token);
  if (!payload) {
    return null;
  }

  const role = payload['role'] ?? payload[ROLE_CLAIM];
  return role === 'Admin' || role === 'User' ? role : null;
}
