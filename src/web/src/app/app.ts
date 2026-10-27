import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';

type Service = {
  name: string;
  description: string;
  healthUrl: string;
  icon: string;
};

@Component({
  selector: 'app-root',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatIconModule,
    MatListModule,
    MatSidenavModule,
    MatToolbarModule,
    RouterOutlet,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('web');

  protected readonly services: Service[] = [
    {
      name: 'Spaces',
      description: 'Catalogo di aule, laboratori e sale studio.',
      healthUrl: '/api/spaces/health',
      icon: 'location_city',
    },
    {
      name: 'Bookings',
      description: 'Prenotazioni senza sovrapposizioni.',
      healthUrl: '/api/bookings/health',
      icon: 'event_available',
    },
    {
      name: 'Users',
      description: 'Profili sintetici e ruoli del campus.',
      healthUrl: '/api/users/health',
      icon: 'people',
    },
  ];
}
