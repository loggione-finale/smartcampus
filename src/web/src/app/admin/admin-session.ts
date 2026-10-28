// Temporary: replace with the real login/session service (#82) when it lands in main.
// The role and the JWT issued by login are read from sessionStorage.
function read(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

export const currentRole = (): string | null => read('smartcampus.role');
export const currentToken = (): string | null => read('smartcampus.token');
