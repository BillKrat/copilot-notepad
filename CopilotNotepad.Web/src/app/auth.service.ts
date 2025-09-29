import { Injectable } from '@angular/core';
import { createAuth0Client, Auth0Client, User } from '@auth0/auth0-spa-js';
import { BehaviorSubject, Observable, from, of } from 'rxjs';
import { switchMap, tap, catchError } from 'rxjs/operators';
import { AUTH0_CONFIG } from './auth0.config';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private auth0Client!: Auth0Client;
  private isAuthenticatedSubject$ = new BehaviorSubject<boolean>(false);
  private userSubject$ = new BehaviorSubject<User | null>(null);

  public isAuthenticated$ = this.isAuthenticatedSubject$.asObservable();
  public user$ = this.userSubject$.asObservable();

  constructor() {
    this.initializeAuth0();
  }

  private async initializeAuth0(): Promise<void> {
    this.auth0Client = await createAuth0Client({
      domain: AUTH0_CONFIG.domain,
      clientId: AUTH0_CONFIG.clientId,
      authorizationParams: {
        audience: AUTH0_CONFIG.audience,
        redirect_uri: AUTH0_CONFIG.redirectUri
      }
    });

    await this.checkAuthentication();
  }

  private async checkAuthentication(): Promise<void> {
    try {
      const isAuthenticated = await this.auth0Client.isAuthenticated();
      this.isAuthenticatedSubject$.next(isAuthenticated);

      if (isAuthenticated) {
        const user = await this.auth0Client.getUser();
        this.userSubject$.next(user || null);
      }
    } catch (error) {
      console.error('Error checking authentication:', error);
    }
  }

  login(): Observable<void> {
    return from(this.auth0Client.loginWithRedirect());
  }

  logout(): void {
    this.auth0Client.logout({
      logoutParams: {
        returnTo: window.location.origin
      }
    });
  }

  handleAuthCallback(): Observable<void> {
    return from(this.auth0Client.handleRedirectCallback()).pipe(
      tap(() => this.checkAuthentication()),
      switchMap(() => of(undefined)),
      catchError(err => {
        console.error('Error handling auth callback:', err);
        return of(undefined);
      })
    );
  }

  getAccessToken(): Observable<string> {
    return from(this.auth0Client.getTokenSilently());
  }
}