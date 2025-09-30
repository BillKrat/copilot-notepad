import { Component, Inject, ViewChild } from '@angular/core';
import { AuthService } from '@auth0/auth0-angular';
import { DOCUMENT } from '@angular/common';
import { OverlayPanel } from 'primeng/overlaypanel';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  standalone: false,
  styleUrl: './login.component.css'
})
export class LoginComponent {
  
  @ViewChild('userMenu') userMenu!: OverlayPanel;
  
  constructor(
    @Inject(DOCUMENT) public document: Document,
    public auth: AuthService
  ) {}

  login(): void {
    this.auth.loginWithRedirect();
  }

  logout(): void {
    this.auth.logout({ logoutParams: { returnTo: this.document.location.origin } });
  }

  loginWithPopup(): void {
    this.auth.loginWithPopup();
  }

  toggleMenu(event: Event): void {
    this.userMenu.toggle(event);
  }
}
