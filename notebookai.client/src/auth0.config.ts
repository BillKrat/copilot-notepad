import { environment } from "./environments/environment";

// Define common token options to avoid repetition
const defaultTokenOptions = {
  tokenOptions: {
    authorizationParams: {
      audience: environment.auth0.audience,
    }
  }
};

// Define API endpoints based on environment  TODO: lose the hardcoded urls
const apiEndpoints = [
  '/weatherforecast',
  '/api/*',
  ...(environment.production 
    ? ['https://api.global-webnet.com/*']
    : ['https://localhost:7280/*']
  )
];

export const authConfig = {
  domain: environment.auth0.domain,
  clientId: environment.auth0.clientId,
  authorizationParams: {
    redirect_uri: window.location.origin,
    audience: environment.auth0.audience,
  },
  httpInterceptor: {
    allowedList: apiEndpoints.map(uri => ({
      uri,
      ...defaultTokenOptions
    }))
  }
};
