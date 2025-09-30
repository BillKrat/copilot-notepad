// Auto-populate Bearer token after OAuth2 authentication
window.addEventListener('load', function () {
    console.log('Swagger auth helper loaded');

    // Wait for Swagger UI to fully initialize
    setTimeout(() => {
        // Monitor for OAuth2 authentication completion
        const checkForToken = setInterval(() => {
            try {
                if (!window.ui || !window.ui.getState) return;

                const state = window.ui.getState();
                const authState = state.get('auth');
                let oauth2Token = null;

                if (authState) {
                    const authorized = authState.get ? authState.get('authorized') : authState.authorized;
                    if (authorized) {
                        const oauth2Auth = authorized.get ? authorized.get('OAuth2') : authorized.OAuth2;
                        if (oauth2Auth) {
                            if (oauth2Auth.get) {
                                oauth2Token = oauth2Auth.get('token')?.get('access_token') ||
                                             oauth2Auth.get('access_token') ||
                                             oauth2Auth.get('value');
                            } else {
                                oauth2Token = oauth2Auth.token?.access_token ||
                                             oauth2Auth.access_token ||
                                             oauth2Auth.value;
                            }
                        }
                    }
                }

                if (oauth2Token) {
                    console.log('OAuth2 token detected, auto-populating Bearer field...');

                    // Auto-populate the Bearer token
                    window.ui.authActions.authorize({
                        Bearer: {
                            name: 'Bearer',
                            schema: {
                                type: 'apiKey',
                                in: 'header',
                                name: 'Authorization'
                            },
                            value: `Bearer ${oauth2Token}`
                        }
                    });

                    console.log('Bearer token automatically populated!');
                    clearInterval(checkForToken);

                    // Show success notification
                    setTimeout(() => {
                        const notification = document.createElement('div');
                        notification.textContent = '✅ Bearer token auto-populated!';
                        notification.style.cssText = `
                            position: fixed; top: 20px; right: 20px; background: #49cc90;
                            color: white; padding: 10px 15px; border-radius: 4px; z-index: 10000;
                            font-family: sans-serif; box-shadow: 0 2px 8px rgba(0,0,0,0.2);
                        `;
                        document.body.appendChild(notification);
                        setTimeout(() => {
                            if (document.body.contains(notification)) {
                                document.body.removeChild(notification);
                            }
                        }, 3000);
                    }, 500);
                }
            } catch (error) {
                console.error('Error checking for OAuth2 token:', error);
            }
        }, 1000);

        // Stop checking after 60 seconds
        setTimeout(() => clearInterval(checkForToken), 60000);
    }, 2000);
});