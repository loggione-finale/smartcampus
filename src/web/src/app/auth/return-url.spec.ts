import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it('accepts internal paths with query strings', () => {
    expect(safeReturnUrl('/account')).toBe('/account');
    expect(safeReturnUrl('/spaces?filtro=aula')).toBe('/spaces?filtro=aula');
  });

  it('rejects absolute and protocol-relative URLs', () => {
    expect(safeReturnUrl('https://evil.example')).toBeNull();
    expect(safeReturnUrl('//evil.example')).toBeNull();
    expect(safeReturnUrl('/\\evil.example')).toBeNull();
    expect(safeReturnUrl('javascript:alert(1)')).toBeNull();
  });

  it('rejects the login page to avoid a redirect loop', () => {
    expect(safeReturnUrl('/login')).toBeNull();
    expect(safeReturnUrl('/login?reason=expired')).toBeNull();
    expect(safeReturnUrl('/%6Cogin')).toBeNull();
  });

  it('rejects percent-encoded protocol-relative paths', () => {
    expect(safeReturnUrl('/%2F%2Fevil.example')).toBeNull();
    expect(safeReturnUrl('/%5Cevil.example')).toBeNull();
  });

  it('rejects control characters and empty values', () => {
    expect(safeReturnUrl('')).toBeNull();
    expect(safeReturnUrl(null)).toBeNull();
    expect(safeReturnUrl('/account\u0000')).toBeNull();
  });
});
