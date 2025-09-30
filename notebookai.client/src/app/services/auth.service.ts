import { Injectable } from '@angular/core';
import { AuthService as Auth0Service } from '@auth0/auth0-angular';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  
  constructor(private auth0: Auth0Service) {}

  // Expose Auth0 observables
  get isAuthenticated$(): Observable<boolean> {
    return this.auth0.isAuthenticated$;
  }

  get user$(): Observable<any> {
    return this.auth0.user$;
  }

  get isLoading$(): Observable<boolean> {
    return this.auth0.isLoading$;
  }

  get error$(): Observable<any> {
    return this.auth0.error$;
  }

  // Auth methods
  login(): void {
    this.auth0.loginWithRedirect();
  }

  loginWithPopup(): void {
    this.auth0.loginWithPopup();
  }

  logout(): void {
    this.auth0.logout({ logoutParams: { returnTo: window.location.origin } });
  }

  // Get access token
  getAccessToken(): Observable<string> {
    return this.auth0.getAccessTokenSilently();
  }

  // Check if user has specific role or permission
  hasPermission(permission: string): Observable<boolean> {
    return this.user$.pipe(
      map(user => {
        if (!user) return false;
        const permissions = user['permissions'] || [];
        return permissions.includes(permission);
      })
    );
  }

  // Check if user has specific role
  hasRole(role: string): Observable<boolean> {
    return this.user$.pipe(
      map(user => {
        if (!user) return false;
        const roles = user['https://your-app.com/roles'] || [];
        return roles.includes(role);
      })
    );
  }

  // Get user profile
  getUserProfile(): Observable<any> {
    return this.user$;
  }
}
