import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, ActivatedRouteSnapshot, Router, RouterStateSnapshot, provideRouter } from '@angular/router';
import { SpaceDto } from '../spaces/spaces.models';
import { SpaceFormPage } from './space-form.page';
import { SpacesAdminListPage } from './spaces-admin-list.page';
import { StaffAccess, adminGuard } from './staff-access';

const SPACE: SpaceDto = {
  id: 'space-1',
  name: 'Aula 101',
  type: 'Classroom',
  mode: 'Dedicated',
  building: 'Edificio A',
  floor: 1,
  capacity: 30,
  isActive: true,
  features: { projector: true },
  equipment: ['videoprojector'],
  unavailabilities: [],
  updatedAt: '2026-10-26T08:00:00Z',
};

describe('adminGuard', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  it('redirects to home when there is no role', () => {
    const result = runGuard();
    expect(TestBed.inject(Router).serializeUrl(result as never)).toBe('/');
  });

  it('redirects a student', () => {
    TestBed.inject(StaffAccess).role.set('User');
    expect(runGuard()).not.toBe(true);
  });

  it('lets an admin through', () => {
    TestBed.inject(StaffAccess).role.set('Admin');
    expect(runGuard()).toBe(true);
  });
});

describe('SpacesAdminListPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  it('loads inactive spaces too and deactivates one', () => {
    const fixture = TestBed.createComponent(SpacesAdminListPage);
    fixture.detectChanges();

    const list = http.expectOne((r) => r.url === '/api/spaces/spaces');
    expect(list.request.params.get('includeInactive')).toBe('true');
    list.flush([SPACE]);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Aula 101');

    const toggle = Array.from(element.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Disattiva'),
    )!;
    toggle.click();
    http.expectOne('/api/spaces/spaces/space-1/deactivate').flush({ ...SPACE, isActive: false });
    fixture.detectChanges();

    expect(element.textContent).toContain('Disattivato');
    http.verify();
  });

  it('shows a message when the server refuses', () => {
    const fixture = TestBed.createComponent(SpacesAdminListPage);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/spaces/spaces').flush(null, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Non hai i permessi');
  });
});

describe('SpaceFormPage', () => {
  let http: HttpTestingController;

  function setup(id: string | null) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => id } } } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SpaceFormPage);
    fixture.detectChanges();
    http.expectOne('/api/spaces/spaces/equipment').flush([{ code: 'videoprojector', name: 'Videoproiettore' }]);
    return fixture;
  }

  it('does not send an invalid form', () => {
    const fixture = setup(null);
    (fixture.componentInstance as unknown as { submit(): void }).submit();
    http.verify();
    expect(fixture.componentInstance['form'].touched).toBeTrue();
  });

  it('creates a space with POST', () => {
    const fixture = setup(null);
    const component = fixture.componentInstance as unknown as { form: SpaceFormPage['form']; submit(): void };
    component.form.patchValue({ name: 'Aula 9', building: 'Edificio C', floor: 2, capacity: 40 });
    component.submit();

    const request = http.expectOne('/api/spaces/spaces');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.name).toBe('Aula 9');
    request.flush({ ...SPACE, id: 'new' });
  });

  it('updates an existing space with PUT and keeps its features', () => {
    const fixture = setup('space-1');
    http.expectOne('/api/spaces/spaces/space-1').flush(SPACE);
    const component = fixture.componentInstance as unknown as { submit(): void };
    component.submit();

    const request = http.expectOne('/api/spaces/spaces/space-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.features).toEqual({ projector: true });
    request.flush(SPACE);
  });
});
