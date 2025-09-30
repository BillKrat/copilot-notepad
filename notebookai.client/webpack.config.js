const webpack = require('webpack');
const dotenv = require('dotenv');

module.exports = (config, options) => {
  // Load environment variables from .env file
  const env = dotenv.config().parsed || {};
  
  console.log('Webpack: Loading environment variables:', Object.keys(env));
  
  // Create environment variables object for webpack DefinePlugin
  // Only include custom variables, not NODE_ENV which Angular handles
  const envKeys = Object.keys(env).reduce((prev, next) => {
    prev[`process.env.${next}`] = JSON.stringify(env[next]);
    return prev;
  }, {});

  // Add some helpful debugging info
  envKeys['process.env.BUILD_TIME'] = JSON.stringify(new Date().toISOString());
  envKeys['process.env.WEBPACK_MODE'] = JSON.stringify(options.configuration || 'development');

  console.log('Webpack: DefinePlugin will inject:', Object.keys(envKeys));
  console.log('Webpack: API_URL will be:', env.API_URL || 'undefined');

  // Only add DefinePlugin if we have custom environment variables
  if (Object.keys(envKeys).length > 0) {
    config.plugins.push(
      new webpack.DefinePlugin(envKeys)
    );
  }

  return config;
};
