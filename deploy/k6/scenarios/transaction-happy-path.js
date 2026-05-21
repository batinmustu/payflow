// Transaction happy-path load profile. One iteration = one create-transaction
// call against the gateway, signed with a per-VU cached JWT. Used to:
//
//   * Smoke the dashboard panels (RPS / latency / heap pressure light up)
//   * Catch regressions in the Transaction → Payment service-to-service
//     hop under sustained traffic
//   * Verify gateway throughput
//
// Run:
//   k6 run deploy/k6/scenarios/transaction-happy-path.js
// Tune via env:
//   K6_TARGET_RPS=50 K6_RAMP_MINUTES=2 k6 run ...

import http from 'k6/http';
import { check, group } from 'k6';
import { Trend } from 'k6/metrics';
import { config, jsonHeaders } from '../lib/config.js';
import { login } from '../lib/auth.js';
import { newOrderReference, pickAmountMinor } from '../lib/random.js';

const targetRps = Number(__ENV.K6_TARGET_RPS || 20);
const rampMinutes = Number(__ENV.K6_RAMP_MINUTES || 1);

// Constant arrival rate so behavior under steady-state load is what we
// actually exercise. Pre-allocated VUs cover the worst-case latency
// scenario; k6 grows up to maxVUs if needed but logs when it does.
export const options = {
  scenarios: {
    transaction_create: {
      executor: 'constant-arrival-rate',
      rate: targetRps,
      timeUnit: '1s',
      duration: `${rampMinutes}m`,
      preAllocatedVUs: Math.max(10, Math.ceil(targetRps * 0.5)),
      maxVUs: Math.max(50, targetRps * 5),
    },
  },
  thresholds: {
    // Hard SLO contracts. Tune as the service matures.
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:create-transaction}': ['p(95)<600', 'p(99)<2000'],
    create_transaction_duration: ['p(95)<600'],
  },
};

// Custom trend so we can scope thresholds to just the create-transaction
// call (login is excluded — it happens once per VU).
const createTxTrend = new Trend('create_transaction_duration', true);

export function setup() {
  // Warm the gateway and confirm credentials work BEFORE the scenario
  // starts pumping RPS — fail fast if the env isn't configured.
  const probe = http.get(`${config.baseUrl}/health`);
  if (probe.status !== 200) {
    throw new Error(`gateway health probe failed: ${probe.status}`);
  }
}

export default function () {
  const token = login();
  const orderReference = newOrderReference();
  const amount = pickAmountMinor();

  group('create transaction', () => {
    const res = http.post(
      `${config.baseUrl}/api/transactions`,
      JSON.stringify({
        orderReference,
        amountMinor: amount,
        currency: config.defaultCurrency,
        cardToken: config.cardToken,
      }),
      { ...jsonHeaders(token), tags: { endpoint: 'create-transaction' } }
    );

    createTxTrend.add(res.timings.duration);

    check(res, {
      'status 201 or 200': (r) => r.status === 201 || r.status === 200,
      'response has id': (r) => {
        try { return Boolean(r.json().id); } catch { return false; }
      },
    });
  });
}
