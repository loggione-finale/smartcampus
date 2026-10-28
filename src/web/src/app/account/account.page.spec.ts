import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthSessionService } from '../auth/auth-session.service';
import { LoginResponse } from '../auth/auth.models';
import { AccountPage } from './account.page';

type AccountPageTest = {
  logout(): void;
};

const LOGIN_RESPONSE: LoginResponse = {
  token: 'token-123',
  tokenType: 'Bearer',
  expiresIn: 28_800,
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: {
    id: 'b7e1c2d3-0000-4000-8000-000000000002',
    matricola: '1000000001',
    firstName: 'Giulia',
    lastName: 'Rossi',
    displayName: 'Giulia Rossi',
    email: 'giulia.rossi@smartcampus.test',
    phoneNumber: '+393331122334',
    role: 'User',
    createdAt: '2026-10-27T10:00:00Z',
  },
};

describe('AccountPage', () => {
  let fixture: ComponentFixture<AccountPage>;
  let router: Router;
  let session: AuthSessionService;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AccountPage],
      providers: [provideRouter([])],
    }).compileComponents();

    session = TestBed.inject(AuthSessionService);
    session.store(LOGIN_RESPONSE);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('shows the authenticated user profile', () => {
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Giulia Rossi');
    expect(text).toContain('giulia.rossi@smartcampus.test');
    expect(text).toContain('1000000001');
    expect(text).toContain('Studente');
  });

  it('clears the session and returns to login on logout', () => {
    (fixture.componentInstance as unknown as AccountPageTest).logout();

    expect(session.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { reason: 'logout' },
    });
  });
});
