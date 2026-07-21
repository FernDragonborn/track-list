import { login, renewToken } from '../helpers/auth.js';
import { isOk } from '../helpers/checks.js';

export function loginEndpoint(email, password) {
  const res = login(email, password);
  isOk(res, 'login 200');
  return res;
}

export function renewEndpoint(refreshToken) {
  const res = renewToken(refreshToken);
  isOk(res, 'renew token 200');
  return res;
}
