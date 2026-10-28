export type UserRole = 'User' | 'Admin';

export interface AuthUser {
  id: string;
  matricola: string;
  firstName: string;
  lastName: string;
  displayName: string;
  email: string;
  phoneNumber: string;
  role: UserRole;
  createdAt: string;
}

export interface LoginCredentials {
  identifier: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  tokenType: string;
  expiresIn: number;
  expiresAt: string;
  user: AuthUser;
}
