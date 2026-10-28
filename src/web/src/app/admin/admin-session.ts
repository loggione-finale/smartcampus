// Temporary: replace with the real login/session service (#82) when it lands in main.
const ROLE_KEY = 'smartcampus.role';

export function currentRole(): string | null {
  try {
    return sessionStorage.getItem(ROLE_KEY);
  } catch {
    return null;
  }
}
