// The login flow (#82) keeps these legacy keys in sync with the smartcampus.auth.* keys,
// while authInterceptor adds the Bearer header to /api calls. Remove these helpers when
// every admin page reads the session from AuthSessionService.
function read(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

export const currentRole = (): string | null => read('smartcampus.role');
export const currentToken = (): string | null => read('smartcampus.token');
