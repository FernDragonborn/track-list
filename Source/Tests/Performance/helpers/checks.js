import { check } from 'k6';

export function isOk(res, name = 'status is 200') {
  return check(res, { [name]: (r) => r.status === 200 });
}

export function isStatus(res, code, name) {
  return check(res, { [name || `status is ${code}`]: (r) => r.status === code });
}

export function isCreated(res, name = 'status is 201') {
  return check(res, { [name]: (r) => r.status === 201 });
}

export function isNoContent(res, name = 'status is 204') {
  return check(res, { [name]: (r) => r.status === 204 });
}

export function hasData(res, name = 'response has data') {
  return check(res, {
    [name]: (r) => {
      try {
        return r.json().data !== undefined;
      } catch (_) {
        return false;
      }
    },
  });
}

export function isNotError(res, name = 'no server error') {
  return check(res, { [name]: (r) => r.status < 500 });
}
