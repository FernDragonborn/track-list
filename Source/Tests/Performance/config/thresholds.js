// Threshold definitions per scenario type

export const smokeThresholds = {
  http_req_duration: ['p(50)<100', 'p(95)<300', 'p(99)<500'],
  http_req_failed: ['rate<0.01'],
  'http_req_duration{group:::admin_export}': ['p(95)<1500'],
};

export const loadThresholds = {
  http_req_duration: ['p(50)<100', 'p(95)<300', 'p(99)<1000'],
  http_req_failed: ['rate<0.05'],
  'http_req_duration{group:::admin_export}': ['p(95)<3000'],
  'http_req_duration{group:::write}': ['p(95)<1500'],
};

export const stressThresholds = {
  http_req_duration: ['p(50)<100', 'p(95)<300', 'p(99)<2000'],
  http_req_failed: ['rate<0.10'],
  'http_req_duration{group:::admin_export}': ['p(95)<5000'],
  'http_req_duration{group:::write}': ['p(95)<3000'],
};

export const ultraStressThresholds = {
  http_req_duration: ['p(50)<300', 'p(95)<1000', 'p(99)<5000'],
  http_req_failed: ['rate<0.20'],
  'http_req_duration{group:::write}': ['p(95)<5000'],
};
