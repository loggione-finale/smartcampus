// The booking pages (#33/#38) and the admin pages (#74/#119) read these keys directly.
// Keep the canonical keys and the legacy ones in sync until those pages use AuthSessionService.
export const AUTH_TOKEN_STORAGE_KEY = 'smartcampus.auth.token';
export const AUTH_USER_STORAGE_KEY = 'smartcampus.auth.user';
export const AUTH_EXPIRES_AT_STORAGE_KEY = 'smartcampus.auth.expiresAt';

export const LEGACY_AUTH_TOKEN_STORAGE_KEY = 'smartcampus.token';
export const LEGACY_AUTH_ROLE_STORAGE_KEY = 'smartcampus.role';

function storage(): Storage | null {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

export function readAuthToken(): string | null {
  try {
    return storage()?.getItem(AUTH_TOKEN_STORAGE_KEY) ?? null;
  } catch {
    return null;
  }
}

export function clearAuthToken(): void {
  try {
    const target = storage();
    target?.removeItem(AUTH_TOKEN_STORAGE_KEY);
    target?.removeItem(AUTH_USER_STORAGE_KEY);
    target?.removeItem(AUTH_EXPIRES_AT_STORAGE_KEY);
    target?.removeItem(LEGACY_AUTH_TOKEN_STORAGE_KEY);
    target?.removeItem(LEGACY_AUTH_ROLE_STORAGE_KEY);
  } catch {
    // Storage can be unavailable (privacy mode); nothing to clear then.
  }
}
