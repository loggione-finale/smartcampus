import { Component, input } from '@angular/core';

export type AdminSection = 'dashboard' | 'alerts' | 'report' | 'spaces';

// Links between the staff pages, shown in each page head.
@Component({
  selector: 'app-admin-nav',
  template: `
    <nav class="page-nav" aria-label="Area del personale">
      @for (link of links; track link.id) {
        <a [href]="link.href" [attr.aria-current]="link.id === current() ? 'page' : null">
          {{ link.label }}
        </a>
      }
    </nav>
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
