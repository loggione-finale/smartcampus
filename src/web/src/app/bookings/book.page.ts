import { Component } from '@angular/core';
import { BookingFormComponent } from './booking-form.component';

@Component({
  selector: 'app-book-page',
  imports: [BookingFormComponent],
  templateUrl: './book.page.html',
  styleUrl: './book.page.scss',
})
export class BookPage {}
