import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const GATEWAY_URL = __ENV.GATEWAY_URL || 'http://localhost:8080';
const KEYCLOAK_TOKEN_URL = __ENV.KEYCLOAK_TOKEN_URL
  || 'http://localhost:8081/realms/team-06/protocol/openid-connect/token';
const KEYCLOAK_CLIENT_ID = __ENV.KEYCLOAK_CLIENT_ID || 'shop-web';
const KEYCLOAK_CLIENT_SECRET = __ENV.KEYCLOAK_CLIENT_SECRET || 'change-me';
const KEYCLOAK_USERNAME = __ENV.KEYCLOAK_USERNAME || 'alice';
const KEYCLOAK_PASSWORD = __ENV.KEYCLOAK_PASSWORD || 'alice';

const ROOMS = Number(__ENV.ROOMS || 300);
const READ_RPS = Number(__ENV.READ_RPS || 10);
const DURATION = __ENV.DURATION || '6m';

const bookingsProduced = new Counter('bookings_produced');
const bookingsRejected = new Counter('bookings_rejected');

export const options = {
  scenarios: {
    booking_stream: {
      executor: 'shared-iterations',
      vus: 5,
      iterations: ROOMS,
      maxDuration: DURATION,
      exec: 'createBooking',
    },
    http_read: {
      executor: 'constant-arrival-rate',
      rate: READ_RPS,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 10,
      maxVUs: 40,
      exec: 'readHotels',
    },
  },
  thresholds: {},
};

function getToken() {
  const res = http.post(
    KEYCLOAK_TOKEN_URL,
    {
      grant_type: 'password',
      client_id: KEYCLOAK_CLIENT_ID,
      client_secret: KEYCLOAK_CLIENT_SECRET,
      username: KEYCLOAK_USERNAME,
      password: KEYCLOAK_PASSWORD,
    },
    { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } },
  );
  if (res.status !== 200) {
    throw new Error(`Keycloak token request failed: ${res.status} ${res.body}`);
  }
  return res.json('access_token');
}

export function setup() {
  const token = getToken();
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };

  const roomIds = [];
  const stamp = Date.now();
  for (let i = 0; i < ROOMS; i++) {
    const res = http.post(
      `${GATEWAY_URL}/api/rooms`,
      JSON.stringify({
        hotelId: 1,
        roomNumber: `LOAD-${stamp}-${i}`,
        type: 'Double',
        pricePerNight: 1000,
        maxGuests: 2,
      }),
      { headers },
    );
    if (res.status === 200 || res.status === 201) {
      roomIds.push(res.json('id'));
    }
  }

  if (roomIds.length === 0) {
    throw new Error('setup created no rooms — check auth and /api/rooms');
  }
  console.log(`setup: created ${roomIds.length}/${ROOMS} rooms for the booking stream`);
  return { token, roomIds };
}

export function createBooking(data) {
  const roomId = data.roomIds[__ITER % data.roomIds.length];
  const checkIn = new Date(Date.now() + 86400000).toISOString();
  const checkOut = new Date(Date.now() + 2 * 86400000).toISOString();

  const res = http.post(
    `${GATEWAY_URL}/api/bookings`,
    JSON.stringify({
      guestIds: [1],
      roomId: roomId,
      checkInDate: checkIn,
      checkOutDate: checkOut,
    }),
    {
      headers: { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' },
      tags: { endpoint: 'create_booking' },
    },
  );

  if (res.status === 202) {
    bookingsProduced.add(1);
  } else {
    bookingsRejected.add(1);
  }

  check(res, { 'booking accepted (202)': (r) => r.status === 202 });
}

export function readHotels(data) {
  const res = http.get(`${GATEWAY_URL}/api/hotels`, {
    headers: { Authorization: `Bearer ${data.token}` },
    tags: { endpoint: 'read_hotels' },
  });
  check(res, { 'read ok (200)': (r) => r.status === 200 });
}

export function handleSummary(data) {
  const produced = data.metrics.bookings_produced ? data.metrics.bookings_produced.values.count : 0;
  const rejected = data.metrics.bookings_rejected ? data.metrics.bookings_rejected.values.count : 0;
  const line = '='.repeat(64);
  return {
    stdout: `
${line}
 PRODUCED (202 Accepted)      : ${produced}
 REJECTED (non-202)           : ${rejected}
${line}
 Compare PRODUCED against, in Grafana/Prometheus:
   sum(increase(messages_consumed_total{queue="team-06.saga.booking-confirmed"}[20m]))
 They must match despite the kills — that is no-loss-under-churn.png
${line}
`,
  };
}