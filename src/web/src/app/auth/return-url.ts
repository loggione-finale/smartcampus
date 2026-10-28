const MAX_RETURN_URL_LENGTH = 2048;

/**
 * Accepts only same-origin, internal paths. It rejects absolute URLs, protocol-relative
 * URLs, backslash tricks and the login page itself (which would create a redirect loop).
 */
export function safeReturnUrl(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  const candidate = value.trim();
  if (
    candidate.length === 0 ||
    candidate.length > MAX_RETURN_URL_LENGTH ||
    !candidate.startsWith('/') ||
    candidate.startsWith('//') ||
    candidate.startsWith('/\\') ||
    candidate.includes('\\') ||
    /[\u0000-\u001f\u007f]/.test(candidate)
  ) {
    return null;
  }

  const path = candidate.split(/[?#]/, 1)[0];
  let decodedPath: string;
  try {
    decodedPath = decodeURIComponent(path);
  } catch {
    return null;
  }

  if (
    !decodedPath.startsWith('/') ||
    decodedPath.startsWith('//') ||
    decodedPath.includes('\\') ||
    decodedPath === '/login' ||
    decodedPath.startsWith('/login/')
  ) {
    return null;
  }

  return candidate;
}
