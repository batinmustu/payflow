// Centralised config for every k6 scenario. Override any value via an env
// var, e.g. `BASE_URL=http://staging.payflow.example.com:5050 k6 run ...`.

export const config = {
  // Gateway base URL. All scenarios go through the gateway — no scenario
  // should hit a downstream service directly, so the load profile mirrors
  // what real clients experience.
  baseUrl: __ENV.BASE_URL || 'http://localhost:5050',

  // Demo tenant credentials seeded by Identity on first run.
  // Override per-environment via env vars when running against staging /
  // any non-default seed.
  tenantSlug: __ENV.TENANT_SLUG || 'demo',
  adminEmail: __ENV.ADMIN_EMAIL || 'demo@payflow.local',
  adminPassword: __ENV.ADMIN_PASSWORD || 'demo',

  // Card token resolved by the Payment service's stub providers. Real
  // load against real providers would source these from a tokenized vault.
  cardToken: __ENV.CARD_TOKEN || 'tok_test_visa',

  defaultCurrency: __ENV.CURRENCY || 'TRY',
};

export function jsonHeaders(token) {
  const h = { 'Content-Type': 'application/json' };
  if (token) h.Authorization = `Bearer ${token}`;
  return { headers: h };
}
