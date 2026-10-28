import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ApplicationRef } from '@angular/core';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { AuthSessionService } from '../auth/auth-session.service';
import { authGuard } from '../auth/auth.guard';
import { AUTH_TOKEN_KEY, AcceptInvitationError, InvitationsApiService } from './invitations-api.service';
import { Invitation } from './invitations.models';
import { InvitationsPage } from './invitations.page';

const INVITED: Invitation = {
  id: '00000000-0000-0000-0000-000000000001',
  bookingId: '11111111-1111-1111-1111-111111111111',
  email: 'invitato@smartcampus.test',
  userId: '22222222-2222-2222-2222-222222222222',
  role: 'Participant',
  status: 'Invited',
  expiresAt: '2026-10-30T18:00:00Z',
  respondedAt: null,
};

const ACCEPTED: Invitation = {
  ...INVITED,
  id: '00000000-0000-0000-0000-000000000002',
  status: 'Accepted',
  respondedAt: '2026-10-28T09:00:00Z',
};

describe('InvitationsApiService', () => {
  let service: InvitationsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(InvitationsApiService);
    http = TestBed.inject(HttpTestingController);
    window.sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    window.sessionStorage.clear();
  });

  it('gets my invitations with Bearer token', () => {
    window.sessionStorage.setItem(AUTH_TOKEN_KEY, 'jwt-test-token');
    let result: Invitation[] = [];
    service.getMine().subscribe((invitations) => (result = invitations));

    const req = http.expectOne('/api/invitations/mine');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-test-token');
    req.flush([INVITED]);

    expect(result.length).toBe(1);
    expect(result[0].id).toBe(INVITED.id);
  });

  it('accepts an invitation with POST and returns the updated invitation', () => {
    let result: Invitation | null = null;
    service.accept(INVITED.id).subscribe((invitation) => (result = invitation));

    const req = http.expectOne(`/api/invitations/${INVITED.id}/accept`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...INVITED, status: 'Accepted' });

    expect((result as Invitation | null)?.status).toBe('Accepted');
  });

  it('maps 404 to not-found', () => {
    let error: AcceptInvitationError | null = null;
    service.accept(INVITED.id).subscribe({ error: (e: AcceptInvitationError) => (error = e) });

    const req = http.expectOne(`/api/invitations/${INVITED.id}/accept`);
    req.flush({ title: 'Invitation not found' }, { status: 404, statusText: 'Not Found' });

    expect(error as AcceptInvitationError | null).toBe('not-found');
  });

  it('maps 409 Invitation no longer pending to expired', () => {
    let error: AcceptInvitationError | null = null;
    service.accept(INVITED.id).subscribe({ error: (e: AcceptInvitationError) => (error = e) });

    const req = http.expectOne(`/api/invitations/${INVITED.id}/accept`);
    req.flush(
      { title: 'Invitation no longer pending' },
      { status: 409, statusText: 'Conflict' },
    );

    expect(error as AcceptInvitationError | null).toBe('expired');
  });

  it('maps 409 Overlapping booking to conflict', () => {
    let error: AcceptInvitationError | null = null;
    service.accept(INVITED.id).subscribe({ error: (e: AcceptInvitationError) => (error = e) });

    const req = http.expectOne(`/api/invitations/${INVITED.id}/accept`);
    req.flush({ title: 'Overlapping booking' }, { status: 409, statusText: 'Conflict' });

    expect(error as AcceptInvitationError | null).toBe('conflict');
  });
});

describe('InvitationsPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    window.sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    window.sessionStorage.clear();
  });

  function createPage(id: string | null = null): ComponentFixture<InvitationsPage> {
    void id;
    const fixture = TestBed.createComponent(InvitationsPage);
    fixture.detectChanges();
    return fixture;
  }

  function confirm(
    component: InvitationsPage,
    invitation: Invitation,
  ): void {
    type ConfirmFn = (invitation: Invitation) => void;
    (component as unknown as { confirm: ConfirmFn }).confirm(invitation);
  }

  it('shows the loaded invitation list with status labels and deadlines', () => {
    const fixture = createPage();

    http.expectOne('/api/invitations/mine').flush([INVITED]);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Da confermare');
    expect(el.textContent).toContain('Scadenza:');
    expect(el.textContent).toContain('Conferma');
  });

  it('shows the empty state when there are no invitations', () => {
    const fixture = createPage();

    http.expectOne('/api/invitations/mine').flush([]);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Nessun invito in attesa');
  });

  it('confirms an invitation and updates its status', () => {
    const fixture = createPage();
    const component = fixture.componentInstance;

    http.expectOne('/api/invitations/mine').flush([INVITED]);
    fixture.detectChanges();

    confirm(component, INVITED);

    const postReq = http.expectOne(`/api/invitations/${INVITED.id}/accept`);
    expect(postReq.request.method).toBe('POST');
    postReq.flush({ ...INVITED, status: 'Accepted' });
    fixture.detectChanges();

    expect(component['successMessage']()).toBe('Partecipazione confermata.');
    expect(component['invitations']()[0].status).toBe('Accepted');
  });

  it('shows the expired message when the invitation is no longer pending', () => {
    const fixture = createPage();
    const component = fixture.componentInstance;

    http.expectOne('/api/invitations/mine').flush([INVITED]);
    fixture.detectChanges();

    confirm(component, INVITED);

    http
      .expectOne(`/api/invitations/${INVITED.id}/accept`)
      .flush(
        { title: 'Invitation no longer pending' },
        { status: 409, statusText: 'Conflict' },
      );
    fixture.detectChanges();

    expect(component['errorMessage']()).toBe(
      'Invito scaduto: sei stato escluso dalla prenotazione.',
    );
  });

  it('shows the overlap message on 409 Overlapping booking', () => {
    const fixture = createPage();
    const component = fixture.componentInstance;

    http.expectOne('/api/invitations/mine').flush([INVITED]);
    fixture.detectChanges();

    confirm(component, INVITED);

    http
      .expectOne(`/api/invitations/${INVITED.id}/accept`)
      .flush({ title: 'Overlapping booking' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(component['errorMessage']()).toBe(
      'Hai già una prenotazione che si sovrappone a questo orario.',
    );
  });

  it('shows the not-found message on 404', () => {
    const fixture = createPage();
    const component = fixture.componentInstance;

    http.expectOne('/api/invitations/mine').flush([INVITED]);
    fixture.detectChanges();

    confirm(component, INVITED);

    http
      .expectOne(`/api/invitations/${INVITED.id}/accept`)
      .flush({ title: 'Invitation not found' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(component['errorMessage']()).toBe('Invito non trovato.');
  });

  it('highlights the invitation matching the :id route parameter', () => {
    const fixture = createPage();
    http.expectOne('/api/invitations/mine').flush([]);

    const component = fixture.componentInstance as unknown as {
      highlightedId: { set: (value: string) => void };
      invitations: { set: (value: Invitation[]) => void };
      loading: { set: (value: boolean) => void };
    };
    component.highlightedId.set(INVITED.id);
    component.invitations.set([INVITED, ACCEPTED]);
    component.loading.set(false);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const highlighted = el.querySelectorAll('.invitation-card.highlighted');
    expect(highlighted.length).toBe(1);
    expect(highlighted[0].textContent).toContain('Da confermare');
  });
});

describe('InvitationsPage guard', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<InvitationsPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InvitationsPage],
      providers: [
        provideRouter([
          {
            path: 'invitations',
            canActivate: [authGuard],
            component: InvitationsPage,
          },
          {
            path: 'login',
            // Standalone login stub: the guard only needs a matching route.
            component: InvitationsPage,
          },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    window.sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    window.sessionStorage.clear();
  });

  it('redirects to login when the user is not authenticated', async () => {
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/invitations');

    expect(router.url.startsWith('/login')).toBeTrue();
    expect(router.url).toContain(encodeURIComponent('/invitations'));
  });

  it('allows navigation when the session is valid', async () => {
    const session = TestBed.inject(AuthSessionService);
    session.clear();
    session.store({
      token: 'jwt-test-token',
      tokenType: 'Bearer',
      expiresIn: 28_800,
      expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
      user: {
        id: '22222222-2222-2222-2222-222222222222',
        matricola: '1000000001',
        firstName: 'Giulia',
        lastName: 'Rossi',
        displayName: 'Giulia Rossi',
        email: 'giulia.rossi@smartcampus.test',
        phoneNumber: '+393331122334',
        role: 'User',
        createdAt: '2026-10-27T10:00:00Z',
      },
    });

    expect(session.hasValidSession()).toBeTrue();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/invitations');

    expect(router.url).toBe('/invitations');
  });
});
