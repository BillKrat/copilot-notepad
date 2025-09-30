import { Component } from '@angular/core';
import { PrimeNG } from 'primeng/config';
import Lara from '@primeng/themes/lara';
import { AuthService } from '@auth0/auth0-angular';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'Notepad AI';

  constructor(private primeng: PrimeNG, public auth: AuthService) {
    // Configure PrimeNG theme
    this.primeng.theme.set({
      preset: Lara,
      options: {
        darkModeSelector: '.dark'
      }
    });
  }
}
