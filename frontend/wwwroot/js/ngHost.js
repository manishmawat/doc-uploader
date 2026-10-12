// Hands API access tokens to the Angular app embedded by Pages/MyDocuments.razor.
// The iframe sends { type: 'token-request' }; we reply with { type: 'token', accessToken, expiresOn }
// or { type: 'token-error', message }, only to that iframe and only at its exact origin.

let handler = null;

export function start(iframe, dotNetRef, appOrigin) {
    stop();

    handler = async (event) => {
        if (event.source !== iframe.contentWindow || event.origin !== appOrigin) {
            return;
        }
        if (event.data?.type !== 'token-request') {
            return;
        }

        let reply;
        try {
            const token = await dotNetRef.invokeMethodAsync('GetToken');
            reply = { type: 'token', accessToken: token.accessToken, expiresOn: token.expiresOn };
        } catch (error) {
            reply = { type: 'token-error', message: 'Could not get an access token. Please sign in again.' };
            console.warn('ngHost: token request failed', error);
        }

        iframe.contentWindow?.postMessage(reply, appOrigin);
    };

    window.addEventListener('message', handler);
}

export function stop() {
    if (handler) {
        window.removeEventListener('message', handler);
        handler = null;
    }
}
