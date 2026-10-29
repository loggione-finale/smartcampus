import { TestBed } from '@angular/core/testing';
import { AdminNavComponent } from './admin-nav.component';

describe('AdminNavComponent', () => {
  it('links every staff page and marks the current one', () => {
    const fixture = TestBed.createComponent(AdminNavComponent);
    fixture.componentRef.setInput('current', 'alerts');
    fixture.detectChanges();

    const links = Array.from(fixture.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/admin/dashboard',
      '/admin/alerts',
      '/admin/report',
      '/admin/spaces',
    ]);
    expect(links.filter((a) => a.getAttribute('aria-current') === 'page').map((a) => a.textContent?.trim())).toEqual([
      'Avvisi',
    ]);
  });
});
