// Refund saga load profile. Each iteration drives the full saga:
//   1. Create a transaction.
//   2. POST a refund request against it.
//
// Useful to verify the Reconciliation consumer keeps up with the create
// rate and that the saga choreography stays inside latency targets when
// the bus is hot. Run alongside the Grafana dashboard to watch the
// payflow.refund.* topics churn.
//
// Run:
//   k6 run deploy/k6/scenarios/refund-saga.js
//
// Knobs:
//   K6_TARGET_RPS=10  one refund flow per second
//   K6_DURATION=5m    how long to sustain it

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { config, jsonHeaders } from '../lib/config.js';
import { login } from '../lib/auth.js';
import { newOrderReference, pickAmountMinor } from '../lib/random.js';

const targetRps = Number(__ENV.K6_TARGET_RPS || 5);
const duration = __ENV.K6_DURATION || '2m';

export const options = {
  scenarios: {
    refund_flow: {
      executor: 'constant-arrival-rate',
      rate: targetRps,
      timeUnit: '1s',
      duration,
      preAllocatedVUs: Math.max(10, targetRps * 2),
      maxVUs: Math.max(40, targetRps * 6),
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    'http_req_duration{endpoint:create-transaction}': ['p(95)<800'],
    'http_req_duration{endpoint:request-refund}': ['p(95)<800'],
    saga_kickoff_duration: ['p(95)<1500'],
  },
};

const sagaKickoffTrend = new Trend('saga_kickoff_duration', true);

export default function () {
  const token = login();

  let transactionId = null;

  group('create transaction', () => {
    const res = http.post(
      `${config.baseUrl}/api/transactions`,
      JSON.stringify({
        orderReference: newOrderReference(),
        amountMinor: pickAmountMinor(),
        currency: config.defaultCurrency,
        cardToken: config.cardToken,
      }),
      { ...jsonHeaders(token), tags: { endpoint: 'create-transaction' } }
    );

    const ok = check(res, {
      'tx created': (r) => r.status === 200 || r.status === 201,
    });
    if (!ok) return;

    try { transactionId = res.json().id; } catch { transactionId = null; }
  });

  if (!transactionId) return;

  // Tiny delay so the producer-side outbox has a chance to drain and
  // the Reconciliation saga can pick up the RefundRequested without
  // racing on the same database connection pool.
  sleep(0.1);

  group('request refund', () => {
    const start = Date.now();
    const res = http.post(
      `${config.baseUrl}/api/transactions/${transactionId}/refunds`,
      JSON.stringify({ amountMinor: pickAmountMinor() }),
      { ...jsonHeaders(token), tags: { endpoint: 'request-refund' } }
    );
    sagaKickoffTrend.add(Date.now() - start);

    check(res, {
      'refund accepted': (r) => r.status === 202 || r.status === 200,
    });
  });
}
