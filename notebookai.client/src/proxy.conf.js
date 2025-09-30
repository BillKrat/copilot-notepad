const { env } = require('process');

// Use environment variable or fallback to your local API
const target = env["services__notebookai-server__https__0"] ?? 
               env["API_URL"] ?? 
               'https://localhost:7280';

const PROXY_CONFIG = [
  {
    context: [
      "/weatherforecast",
      "/api/*" // Add this for any future API endpoints
    ],
    target,
    secure: false, // Set to false for local development with self-signed certs
    changeOrigin: true,
    logLevel: "debug"
  }
]

module.exports = PROXY_CONFIG;
