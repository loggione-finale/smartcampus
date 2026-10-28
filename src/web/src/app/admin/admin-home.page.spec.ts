import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthSessionService } from '../auth/auth-session.service';
import { LoginResponse } from '../auth/auth.models';
import { AdminHomePage } from './admin-home.page';

type AdminHomePageTest = {
  logout(): void;
};

const ADMIN_RESPONSE: LoginResponse = {
  token: 'admin-token',
  tokenType: 'Bearer',
  expiresIn: 28_800,
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: {
    id: 'b7e1c2d3-0000-4000-8000-000000000001',
    matricola: '0000000001',
    firstName: 'Admin',
    lastName: 'Campus',
    displayName: 'Admin Campus',
    email: 'admin@smartcampus.test',
    phoneNumber: '+390801234567',
    role: 'Admin',
    createdAt: '2026-10-27T10:00:00Z',
  },
};

describe('AdminHomePage', () => {
  let fixture: ComponentFixture<AdminHomePage>;
  let router: Router;
  let session: AuthSessionService;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AdminHomePage],
      providers: [provideRouter([])],
    }).compileComponents();

    session = TestBed.inject(AuthSessionService);
    session.store(ADMIN_RESPONSE);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(AdminHomePage);
    fixture.detectChanges();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('shows the admin name and the area reserved for the staff', () => {
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Admin Campus');
    expect(text).toContain('Pannello amministratore');
  });

  it('clears the session and returns to login on logout', () => {
    (fixture.componentInstance as unknown as AdminHomePageTest).logout();

    expect(session.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { reason: 'logout' },
    });
  });
});
