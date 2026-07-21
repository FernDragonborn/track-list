import { apiPost } from '../helpers/http.js';
import { isNotError } from '../helpers/checks.js';

const STATUSES = ['planned', 'watching', 'completed', 'dropped'];

export function upsertTracking(token, mediaId) {
  const res = apiPost(
    '/trackingstatus',
    {
      mediaId,
      status: STATUSES[Math.floor(Math.random() * STATUSES.length)],
    },
    token,
    { name: 'tracking_upsert', group: 'write' }
  );
  isNotError(res, 'tracking upsert no 5xx');
  return res;
}
