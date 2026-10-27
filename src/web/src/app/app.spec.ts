import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { App } from './app';

type RegistrationTestApp = {
  registrationForm: FormGroup<{
    matricola: FormControl<string>;
    firstName: FormControl<string>;
    lastName: FormControl<string>;
    email: FormControl<string>;
    phoneNumber: FormControl<string>;
    password: FormControl<string>;
  }>;
  submitRegistration(): void;
};

describe('App', () => {
  let httpTesting: HttpTestingController;
  let fetchSpy: jasmine.Spy;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
    fetchSpy = spyOn(window, 'fetch').and.returnValue(Promise.resolve(new Response(null, { status: 200 })));
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the SmartCampus shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Prenota.');
    expect(compiled.querySelectorAll('.space').length).toBe(4);
    expect(compiled.querySelectorAll('.service').length).toBe(3);
  });

  it('keeps the registration form invalid until all server rules are met', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

    app.registrationForm.patchValue({
      matricola: '123',
      firstName: 'A',
      lastName: '',
      email: 'not-an-email',
      phoneNumber: '3331234567',
      password: 'short',
    });
    app.submitRegistration();
    fixture.detectChanges();

    expect(app.registrationForm.invalid).toBeTrue();
    expect(fixture.nativeElement.querySelectorAll('mat-error').length).toBeGreaterThan(0);
  });

  it('shows a duplicate email response on the email field', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

    app.registrationForm.setValue({
      matricola: '1234567890',
      firstName: 'Mario',
      lastName: 'Rossi',
      email: 'mario@example.test',
      phoneNumber: '+393331234567',
      password: 'Password123',
    });
    app.submitRegistration();

    const request = httpTesting.expectOne('/api/users/auth/register');
    expect(request.request.method).toBe('POST');
    request.flush(
      { detail: 'Email is already registered.' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Email is already registered.');
    expect(fixture.nativeElement.textContent).toContain('Esiste già un account');
  });

  it('does not submit an incomplete booking form', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    app.bookingForm.controls.date.setValue('');

    await app.submitBooking();

    expect(app.bookingState()).toBe('error');
    expect(fetchSpy.calls.all().some((call) => call.args[0] === '/api/bookings')).toBeFalse();
  });

  it('shows the conflict returned by Bookings', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fetchSpy.and.returnValue(Promise.resolve(new Response(JSON.stringify({ title: 'Space already booked' }), { status: 409 })));

    await app.submitBooking();

    expect(app.bookingState()).toBe('error');
    expect(app.bookingMessage()).toContain('già occupata');
  });

  it('shows the reference code after a successful booking', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fetchSpy.and.returnValue(Promise.resolve(new Response(JSON.stringify({ referenceCode: 'SC-261027-ABCD' }), { status: 201 })));

    await app.submitBooking();

    expect(app.bookingState()).toBe('success');
    expect(app.bookingReference()).toBe('SC-261027-ABCD');
  });

  it('validates participant emails and exposes equipment only for groups', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();

    const chipInput = { clear: jasmine.createSpy('clear') };
    app.addParticipant({ value: 'group@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(app.participantEmails()).toEqual(['group@example.test']);
    expect(fixture.nativeElement.querySelector('.equipment-field')).not.toBeNull();

    app.addParticipant({ value: 'group@example.test', chipInput } as any);
    expect(app.participantError()).toContain('già stata aggiunta');

    app.removeParticipant('group@example.test');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.equipment-field')).toBeNull();
  });

  it('sends participants and requested equipment with a group booking', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fetchSpy.and.returnValue(Promise.resolve(new Response(null, { status: 201 })));
    app.addParticipant({ value: 'group@example.test', chipInput: { clear() {} } } as any);
    app.bookingForm.controls.equipment.setValue(['Videoproiettore']);

    await app.submitBooking();

    const bookingCall = fetchSpy.calls.all().find((call) => call.args[0] === '/api/bookings');
    expect(bookingCall).toBeDefined();
    if (!bookingCall) return;
    const request = bookingCall.args[1] as RequestInit;
    expect(JSON.parse(request.body as string)).toEqual(
      jasmine.objectContaining({
        participantEmails: ['group@example.test'],
        equipmentRequests: ['Videoproiettore'],
      }),
    );
  });
});
