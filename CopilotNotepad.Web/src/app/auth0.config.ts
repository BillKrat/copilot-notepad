export interface Auth0Config {
  domain: string;
  clientId: string;
  audience: string;
  redirectUri: string;
}

export const AUTH0_CONFIG: Auth0Config = {
  domain: 'your-auth0-domain.auth0.com',
  clientId: 'your-auth0-client-id',
  audience: 'https://copilot-notepad-api',
  redirectUri: window.location.origin
};