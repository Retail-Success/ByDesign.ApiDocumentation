// ByDesign API v2 - JavaScript (Node.js) sample
//
// Requires: Node.js 18+ (uses the built-in fetch). Run with: node bydesign-api.mjs
// Set these environment variables first (values come from the ByDesign back office):
//   BYDESIGN_API_URL        e.g. https://your-api-url (no trailing slash)
//   BYDESIGN_CLIENT_ID
//   BYDESIGN_CLIENT_SECRET
//   BYDESIGN_API_KEY
//
// Run this server-side only. Never ship your client secret or API key to a browser or mobile app.

/** Minimal ByDesign API client that caches its access token and retries on 401 and 429. */
export class ByDesignClient {
  #accessToken = null;
  #expiresAt = 0;
  #pendingToken = null;

  constructor({ apiUrl, clientId, clientSecret, apiKey, scope = 'api/Read api/Write' }) {
    this.apiUrl = apiUrl.replace(/\/+$/, '');
    this.clientId = clientId;
    this.clientSecret = clientSecret;
    this.apiKey = apiKey;
    this.scope = scope;
  }

  async #getToken() {
    // Reuse the token until one minute before it expires
    if (this.#accessToken && Date.now() < this.#expiresAt - 60_000) {
      return this.#accessToken;
    }
    // Share one in-flight token request between concurrent callers
    this.#pendingToken ??= (async () => {
      const response = await fetch(`${this.apiUrl}/oauth2/token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          grant_type: 'client_credentials',
          client_id: this.clientId,
          client_secret: this.clientSecret,
          scope: this.scope,
        }),
      });
      if (!response.ok) throw new Error(`Token request failed: ${response.status} ${await response.text()}`);
      const token = await response.json();
      this.#accessToken = token.access_token;
      this.#expiresAt = Date.now() + token.expires_in * 1000;
      return this.#accessToken;
    })().finally(() => { this.#pendingToken = null; });
    return this.#pendingToken;
  }

  async request(method, path, body) {
    for (let attempt = 1; ; attempt++) {
      const response = await fetch(`${this.apiUrl}${path}`, {
        method,
        headers: {
          Authorization: `Bearer ${await this.#getToken()}`,
          'x-api-key': this.apiKey,
          ...(body !== undefined && { 'Content-Type': 'application/json' }),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });

      if (response.status === 401 && attempt === 1) {
        this.#accessToken = null; // Token expired or was revoked: fetch a new one and retry once
        continue;
      }
      if (response.status === 429 && attempt <= 3) {
        await new Promise((resolve) => setTimeout(resolve, 2 ** attempt * 1000));
        continue;
      }

      const text = await response.text();
      if (!response.ok) throw new Error(`${method} ${path} failed: ${response.status} ${text}`);
      return text ? JSON.parse(text) : null;
    }
  }

  get(path) {
    return this.request('GET', path);
  }

  post(path, body) {
    return this.request('POST', path, body);
  }
}

for (const name of ['BYDESIGN_API_URL', 'BYDESIGN_CLIENT_ID', 'BYDESIGN_CLIENT_SECRET', 'BYDESIGN_API_KEY']) {
  if (!process.env[name]) throw new Error(`Set the ${name} environment variable.`);
}

const client = new ByDesignClient({
  apiUrl: process.env.BYDESIGN_API_URL,
  clientId: process.env.BYDESIGN_CLIENT_ID,
  clientSecret: process.env.BYDESIGN_CLIENT_SECRET,
  apiKey: process.env.BYDESIGN_API_KEY,
});

// GET request: list inventory categories (requires api/Read)
console.log(await client.get('/api/v2/inventory/categorydetail'));

// POST request: look up customers by email (requires api/Write)
console.log(await client.post('/api/v2/customer/lookup', { Email: 'jane.doe@example.com' }));
