import http from 'k6/http';
import { check, fail } from 'k6';
import { config, jsonHeaders } from './config.js';

// k6 spawns one VU per goroutine; module-level state is per-VU.
// Cached token lives in the VU's globalThis so each VU does one login
// per test and reuses the JWT thereafter.
let cachedToken = null;

/**
 * Returns a valid JWT for the demo tenant. Logs in once per VU; subsequent
 * calls hit the cache. If the API ever rejects the cached token (401 in a
 * scenario), call `clearToken()` and `login()` again.
 */
export function login() {
  if (cachedToken) return cachedToken;

  const res = http.post(
    `${config.baseUrl}/api/auth/login`,
    JSON.stringify({
      tenantSlug: config.tenantSlug,
      email: config.adminEmail,
      password: config.adminPassword,
    }),
    jsonHeaders()
  );

  const ok = check(res, { 'login 200': (r) => r.status === 200 });
  if (!ok) {
    fail(`login failed: status=${res.status} body=${res.body}`);
  }

  const body = res.json();
  cachedToken = body.accessToken || body.token;
  if (!cachedToken) {
    fail(`login response missing token: ${JSON.stringify(body)}`);
  }
  return cachedToken;
}

export function clearToken() {
  cachedToken = null;
}
