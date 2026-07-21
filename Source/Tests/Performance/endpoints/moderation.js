import { apiGet } from '../helpers/http.js';
import { isNotError } from '../helpers/checks.js';

export function getPendingTranslations(modToken, pageNumber = 1, pageSize = 20) {
  const res = apiGet(
    `/moderation/translations?pageNumber=${pageNumber}&pageSize=${pageSize}`,
    modToken,
    { name: 'moderation_translations', group: 'moderation' }
  );
  isNotError(res, 'moderation translations no 5xx');
  return res;
}
