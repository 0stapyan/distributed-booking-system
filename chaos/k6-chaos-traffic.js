import http from 'k6/http';
import { check } from 'k6';
import { Counter, Rate } from 'k6/metrics';

const GATEWAY = __ENV.GATEWAY_URL || 'https://team-06.api.34.116.253.250.nip.io';
const TOKEN_URL = __ENV.TOKEN_URL
  || 'https://keycloak.34.116.253.250.nip.io/realms/team-06/protocol/openid-connect/token';
const CLIENT_ID = __ENV.CLIENT_ID || 'shop-web';
const CLIENT_SECRET = __ENV.CLIENT_SECRET || 'change-me';
const USERNAME = __ENV.USERNAME || 'alice';
const PASSWORD = __ENV.PASSWORD || 'alice';

const guestDegraded = new Counter('guest_degraded');
const paymentDegraded = new Counter('payment_degraded');
const bothOk = new Counter('both_legs_ok');
const okRate = new Rate('fanout_success');

export const options = {
  scenarios: {
    fanout: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RPS || 4),
      timeUnit: '1s',
      duration: __ENV.DURATION || '3m',
      preAllocatedVUs: 20,
      maxVUs: 60,
    },
  },
  thresholds: {},
  insecureSkipTLSVerify: true,
};

export function setup() {
  const res = http.post(TOKEN_URL, {
    grant_type: 'password',
    client_id: CLIENT_ID,
    client_secret: CLIENT_SECRET,
    username: USERNAME,
    password: PASSWORD,
  }, { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });

  if (res.status !== 200) throw new Error(`Keycloak token failed: ${res.status} ${res.body}`);
  return { token: res.json('access_token') };
}

export default function (data) {
  const res = http.post(
    `${GATEWAY}/api/guest-score`,
    JSON.stringify({ guestId: 1, roomId: 1, amount: 100 }),
    {
      headers: { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' },
      timeout: '15s',
      tags: { endpoint: 'guest_score' },
    },
  );


  const served = res.status === 200 || res.status === 402;
  okRate.add(served);
  check(res, { 'fan-out served (200/402)': (r) => r.status === 200 || r.status === 402 });

  if (!served) return;

  let body;
  try { body = res.json(); } catch (e) { return; }

  const gBad = body.guest && body.guest.exists === false;
  const pBad = body.payment && body.payment.totalPayments === 0;

  if (gBad) guestDegraded.add(1);
  if (pBad) paymentDegraded.add(1);
  if (!gBad && !pBad) bothOk.add(1);
}