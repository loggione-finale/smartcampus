import { Component, input } from '@angular/core';

export type AdminSection = 'dashboard' | 'alerts' | 'report' | 'spaces';

// Links between the staff pages: without them alerts, report and spaces were reachable only by URL.
@Component({
  selector: 'app-admin-nav',
  template: `
    <nav class="admin-nav" aria-label="Area del personale">
      @for (link of links; track link.id) {
        <a [href]="link.href" [attr.aria-current]="link.id === current() ? 'page' : null">
          {{ link.label }}
        </a>
      }
    </nav>
  `,
  styles: `
    .admin-nav {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 28px;
      margin-bottom: 8px;
    }

    a {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      font-weight: 600;
      color: var(--brand);
      text-decoration: none;
      text-underline-offset: 6px;

      &:hover,
      &:focus-visible,
      &[aria-current='page'] {
        text-decoration: underline 2px;
      }
    }

    a[aria-current='page'] {
      font-weight: 800;
      color: var(--ink);
    }
  `,
})
export class AdminNavComponent {
  readonly current = input.required<AdminSection>();

  protected readonly links: { id: AdminSection; label: string; href: string }[] = [
    { id: 'dashboard', label: 'Dashboard aule', href: '/admin/dashboard' },
    { id: 'alerts', label: 'Avvisi', href: '/admin/alerts' },
    { id: 'report', label: 'Report', href: '/admin/report' },
    { id: 'spaces', label: 'Gestione spazi', href: '/admin/spaces' },
  ];
}
