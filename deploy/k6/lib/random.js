// Tiny helpers so each VU/iteration produces deterministically-unique
// values without colliding with other VUs.

import { randomString } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export function newOrderReference() {
  // VU + iteration + a short random suffix → globally unique across the
  // run, but cheap to compute.
  const stamp = `${Date.now().toString(36)}-${__VU}-${__ITER}`;
  return `k6-${stamp}-${randomString(4)}`;
}

export function pickAmountMinor() {
  // Spread amounts across two orders of magnitude so the histogram
  // panels actually have variety.
  const tiers = [499, 1299, 4999, 9999, 24999, 99999, 249999];
  return tiers[Math.floor(Math.random() * tiers.length)];
}
