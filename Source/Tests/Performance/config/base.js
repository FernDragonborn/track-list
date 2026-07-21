// Base configuration for k6 performance tests

export const BASE_URL = __ENV.BASE_URL || 'http://localhost';
export const API_URL = `${BASE_URL}/api`;

export const DEFAULT_HEADERS = {
  'Content-Type': 'application/json',
};

export function withDefaults(options) {
  return Object.assign(
    {
      insecureSkipTLSVerify: true,
      noConnectionReuse: false,
    },
    options
  );
}
