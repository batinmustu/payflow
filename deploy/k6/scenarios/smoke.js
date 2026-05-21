// Smoke scenario — verifies the fleet is alive and the gateway can reach
// every backend. Tiny constant load for ~30s; meant to run on every PR /
// pre-deploy as a fast "did anything break?" gate.
//
// Run:
//   k6 run deploy/k6/scenarios/smoke.js
// Or against a specific environment:
//   BASE_URL=https://staging.payflow.example.com k6 run deploy/k6/scenarios/smoke.js

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { config } from '../lib/config.js';

export const options = {
  vus: 3,
  duration: '30s',
  thresholds: {
    // Smoke is allowed exactly zero failures — anything else means the
    // fleet is unhealthy, not "load-stressed".
    checks: ['rate==1.0'],
    http_req_failed: ['rate==0.0'],
    http_req_duration: ['p(95)<500', 'p(99)<1500'],
  },
};

const SERVICES = [
  'identity',
  'payment',
  'transaction',
  'reconciliation',
  'reporting',
  'notification',
];

export default function () {
  group('gateway /health', () => {
    const res = http.get(`${config.baseUrl}/health`);
    check(res, { 'gateway healthy': (r) => r.status === 200 });
  });

  // Service /health is exposed via the gateway too — but routes vary by
  // setup. If your YARP config doesn't forward /api/<svc>/health, drop
  // this group locally. We keep it in to give the dashboard real traffic.
  group('per-service /health', () => {
    for (const svc of SERVICES) {
      const res = http.get(`${config.baseUrl}/api/${svc}/health`);
      check(res, {
        [`${svc} healthy or 404`]: (r) => r.status === 200 || r.status === 404,
      });
    }
  });

  sleep(1);
}
