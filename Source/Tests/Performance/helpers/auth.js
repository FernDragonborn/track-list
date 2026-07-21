import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { API_URL, DEFAULT_HEADERS } from '../config/base.js';
import { USERS } from '../config/test-users.js';

export function login(email, password) {
  const res = http.post(
    `${API_URL}/auth/login`,
    JSON.stringify({ email, username: '', password }),
    { headers: DEFAULT_HEADERS, tags: { name: 'auth_login' } }
  );
  return res;
}

export function renewToken(refreshToken) {
  const res = http.post(
    `${API_URL}/auth/renewToken`,
    JSON.stringify({ refreshToken }),
    { headers: DEFAULT_HEADERS, tags: { name: 'auth_renew' } }
  );
  return res;
}

export function authHeaders(accessToken) {
  return {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
    },
  };
}

// Extract tokens from login response: { data: { accessToken, refreshToken } }
function extractTokens(res) {
  try {
    const body = res.json();
    return {
      accessToken: body.data.accessToken,
      refreshToken: body.data.refreshToken,
    };
  } catch (_) {
    return null;
  }
}

// Login all personas, return tokens map. Call from setup().
export function setupAuth() {
  const tokens = {};

  for (const [role, creds] of Object.entries(USERS)) {
    const res = login(creds.email, creds.password);
    const ok = check(res, {
      [`${role} login status 200`]: (r) => r.status === 200,
    });

    if (!ok) {
      console.error(`Auth failed for ${role}: ${res.status} ${res.body}`);
      exec.test.abort(`Cannot authenticate ${role} user`);
    }

    tokens[role] = extractTokens(res);
  }

  return tokens;
}
