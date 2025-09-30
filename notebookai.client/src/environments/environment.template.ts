export const environment = {
  production: false,
  useProxy: false,
  apiUrl: 'https://localhost:7280', // Production API URL
  auth0: {
    domain: 'your-tenant.region.auth0.com',
    clientId: 'your-client-id',
    audience: 'your-audience-url',
  }
};

// Copy this file to environment.ts and update with your actual values
