import { Injectable, computed, signal } from '@angular/core';
import {
  AUTH_EXPIRES_AT_STORAGE_KEY,
  AUTH_TOKEN_STORAGE_KEY,
  AUTH_USER_STORAGE_KEY,
  LEGACY_AUTH_ROLE_STORAGE_KEY,
  LEGACY_AUTH_TOKEN_STORAGE_KEY,
  clearAuthToken,
  readAuthToken,
} from './auth-session';
import { AuthUser, LoginResponse, UserRole } from './auth.models';
import { roleFromToken } from './jwt';

export const AUTH_CLOCK_SKEW_MS = 30_000;

@Injectable({
  providedIn: 'root',
})
export class AuthSessionService {
  private readonly tokenState = signal<string | null>(null);
  private readonly userState = signal<AuthUser | null>(null);
  private readonly expiresAtState = signal<number | null>(null);
  private readonly roleState = signal<UserRole | null>(null);
  private expiredNotice = false;

  readonly token = this.tokenState.asReadonly();
  readonly user = this.userState.asReadonly();
  readonly expiresAt = this.expiresAtState.asReadonly();
  readonly role = this.roleState.asReadonly();
  readonly isAuthenticated = computed(
    () => this.tokenState() !== null && this.userState() !== null,
  );
  readonly isAdmin = computed(() => this.roleState() === 'Admin');

  constructor() {
    this.restore();
  }

  hasSession(): boolean {
    return this.tokenState() !== null && this.userState() !== null;
  }

  isExpired(now: number = Date.now()): boolean {
    const expiresAt = this.expiresAtState();
    return expiresAt === null || expiresAt <= now + AUTH_CLOCK_SKEW_MS;
  }

  hasValidSession(now: number = Date.now()): boolean {
    if (!this.hasSession()) {
      return false;
    }

    if (this.isExpired(now)) {
      this.expire();
      return false;
    }

    return true;
  }

  validToken(now: number = Date.now()): string | null {
    if (!this.hasSession() || this.isExpired(now)) {
      return null;
    }

    return this.tokenState();
  }

  consumeExpiredNotice(): boolean {
    const expired = this.expiredNotice;
    this.expiredNotice = false;
    return expired;
  }

  store(response: LoginResponse): boolean {
    if (!response.token || !response.user?.id) {
      this.clear();
      return false;
    }

    const expiresAt = this.parseExpiry(response);
    if (!Number.isFinite(expiresAt)) {
      this.clear();
      return false;
    }

    const role = roleFromToken(response.token) ?? this.normalizeRole(response.user.role);
    const user: AuthUser = { ...response.user, role };

    this.tokenState.set(response.token);
    this.userState.set(user);
    this.expiresAtState.set(expiresAt);
    this.roleState.set(role);
    this.expiredNotice = false;

    const target = this.storage();
    if (!target) {
      return true;
    }

    try {
      target.setItem(AUTH_TOKEN_STORAGE_KEY, response.token);
      target.setItem(AUTH_USER_STORAGE_KEY, JSON.stringify(user));
      target.setItem(AUTH_EXPIRES_AT_STORAGE_KEY, String(expiresAt));
      target.setItem(LEGACY_AUTH_TOKEN_STORAGE_KEY, response.token);
      target.setItem(LEGACY_AUTH_ROLE_STORAGE_KEY, role);
    } catch {
      // The in-memory state still works for the current page.
    }

    return true;
  }

  clear(): void {
    this.resetState();
    this.expiredNotice = false;
    clearAuthToken();
  }

  private expire(): void {
    this.resetState();
    this.expiredNotice = true;
    clearAuthToken();
  }

  private resetState(): void {
    this.tokenState.set(null);
    this.userState.set(null);
    this.expiresAtState.set(null);
    this.roleState.set(null);
  }

  private restore(): void {
    const token = readAuthToken();
    const target = this.storage();
    if (!token || !target) {
      return;
    }

    try {
      const rawUser = target.getItem(AUTH_USER_STORAGE_KEY);
      const rawExpiresAt = target.getItem(AUTH_EXPIRES_AT_STORAGE_KEY);
      if (!rawUser || !rawExpiresAt) {
        this.clear();
        return;
      }

      const parsedUser = JSON.parse(rawUser) as AuthUser;
      const expiresAt = Number(rawExpiresAt);
      if (!this.isValidUser(parsedUser) || !Number.isFinite(expiresAt)) {
        this.clear();
        return;
      }

      const role = roleFromToken(token) ?? this.normalizeRole(parsedUser.role);
      this.tokenState.set(token);
      this.userState.set({ ...parsedUser, role });
      this.expiresAtState.set(expiresAt);
      this.roleState.set(role);

      if (this.isExpired()) {
        this.expire();
      }
    } catch {
      this.clear();
    }
  }

  private parseExpiry(response: LoginResponse): number {
    const parsed = Date.parse(response.expiresAt);
    if (Number.isFinite(parsed)) {
      return parsed;
    }

    if (typeof response.expiresIn === 'number' && Number.isFinite(response.expiresIn)) {
      return Date.now() + Math.max(0, response.expiresIn) * 1000;
    }

    return Number.NaN;
  }

  private normalizeRole(role: string): UserRole {
    return role === 'Admin' ? 'Admin' : 'User';
  }

  private isValidUser(value: unknown): value is AuthUser {
    if (!value || typeof value !== 'object') {
      return false;
    }

    const user = value as Partial<AuthUser>;
    return (
      typeof user.id === 'string' &&
      typeof user.displayName === 'string' &&
      typeof user.email === 'string' &&
      (user.role === 'User' || user.role === 'Admin')
    );
  }

  private storage(): Storage | null {
    try {
      return window.sessionStorage;
    } catch {
      return null;
    }
  }
}
