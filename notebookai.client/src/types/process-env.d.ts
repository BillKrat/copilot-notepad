// Type declarations for environment variables
declare var process: {
  env: {
    [key: string]: string | undefined;
    NODE_ENV?: string;
    API_URL?: string;
    AUTH0_DOMAIN?: string;
    AUTH0_CLIENT_ID?: string;
    AUTH0_AUDIENCE?: string;
    USE_PROXY?: string;
    DEBUG_MODE?: string;
    ENVIRONMENT?: string;
    BUILD_TIME?: string;
    WEBPACK_MODE?: string;
  };
};
