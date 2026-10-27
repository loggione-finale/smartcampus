import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();
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

  it('does not submit an incomplete booking form', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as App & { bookingForm: any; bookingState: any; submitBooking: () => Promise<void> };
    app.bookingForm.controls.date.setValue('');
    const fetchSpy = spyOn(window, 'fetch');

    await app.submitBooking();

    expect(app.bookingState()).toBe('error');
    expect(fetchSpy).not.toHaveBeenCalledWith('/api/bookings', jasmine.anything());
  });

  it('shows the conflict returned by Bookings', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as App & { bookingState: any; bookingMessage: any; submitBooking: () => Promise<void> };
    spyOn(window, 'fetch').and.returnValue(
      Promise.resolve(new Response(JSON.stringify({ title: 'Space already booked' }), { status: 409 })),
    );

    await app.submitBooking();

    expect(app.bookingState()).toBe('error');
    expect(app.bookingMessage()).toContain('già occupata');
  });

  it('shows the reference code after a successful booking', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as App & { bookingState: any; bookingReference: any; submitBooking: () => Promise<void> };
    spyOn(window, 'fetch').and.returnValue(
      Promise.resolve(new Response(JSON.stringify({ referenceCode: 'SC-261027-ABCD' }), { status: 201 })),
    );

    await app.submitBooking();

    expect(app.bookingState()).toBe('success');
    expect(app.bookingReference()).toBe('SC-261027-ABCD');
  });
});
