import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:3050';
const ORIGIN = __ENV.ORIGIN || 'http://localhost:3050';
const ACCOUNT_NUMBER = __ENV.ACCOUNT_NUMBER || '12345-6';

export const options = {
  scenarios: {
    consolidated_balance_peak: {
      executor: 'constant-arrival-rate',
      rate: 55, // 55 req/s sustentados (acima do mínimo exigido de 50 req/s)
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 50,
      maxVUs: 100,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'], // Critério oficial: perda máxima de 5% (meta: 0%)
    http_req_duration: ['p(95)<200', 'p(99)<500'],
  },
};

export function setup() {
  const headers = {
    'Content-Type': 'application/json',
    Origin: ORIGIN,
  };

  // Aquecimento: cria lançamento inicial na conta 12345-6
  const payload = JSON.stringify({
    accountNumber: ACCOUNT_NUMBER,
    value: 100.0,
    transactionType: 1,
    idempotencyKey: `warmup-${Date.now()}`,
  });

  const txRes = http.post(`${BASE_URL}/api/transaction/v1`, payload, { headers });
  check(txRes, {
    'setup transaction created (201/200)': (r) => r.status === 201 || r.status === 200,
  });

  // Aguarda OutboxRelay (500ms) + ConsolidationWorker processarem o evento assíncrono
  sleep(1.5);

  const warmRes = http.get(
    `${BASE_URL}/api/consolidatedstatement/v1?AccountNumber=${ACCOUNT_NUMBER}`,
    { headers: { Origin: ORIGIN } }
  );
  check(warmRes, {
    'setup consolidated warmup (200)': (r) => r.status === 200,
  });
}

export default function () {
  const res = http.get(
    `${BASE_URL}/api/consolidatedstatement/v1?AccountNumber=${ACCOUNT_NUMBER}`,
    {
      headers: {
        Origin: ORIGIN,
      },
    }
  );

  check(res, {
    'status is 200 OK': (r) => r.status === 200,
    'contains consolidatedBalance': (r) => r.body && r.body.includes('consolidatedBalance'),
  });
}
