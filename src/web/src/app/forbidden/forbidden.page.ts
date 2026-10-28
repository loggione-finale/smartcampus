import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-forbidden-page',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './forbidden.page.html',
  styleUrl: './forbidden.page.scss',
})
export class ForbiddenPage {}
