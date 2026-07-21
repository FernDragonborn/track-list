import { apiGet, apiPost } from '../helpers/http.js';
import { isOk, isNotError } from '../helpers/checks.js';

export function getPublicCollections(pageNumber = 1, pageSize = 10) {
  const res = apiGet(
    `/collections/public?pageNumber=${pageNumber}&pageSize=${pageSize}`,
    null,
    { name: 'collections_public', group: 'feed' }
  );
  isOk(res, 'public collections 200');
  return res;
}

export function createCollection(token) {
  const res = apiPost(
    '/collections',
    {
      name: `PerfColl-${Date.now()}`,
      description: 'Навантажувальне тестування колекцій',
      privacyLevel: 'public',
    },
    token,
    { name: 'collection_create', group: 'write' }
  );
  isNotError(res, 'collection create no 5xx');
  return res;
}

export function addItem(token, collectionId, mediaId) {
  const res = apiPost(
    `/collections/${collectionId}/items`,
    { mediaId, order: null },
    token,
    { name: 'collection_add_item', group: 'write' }
  );
  isNotError(res, 'collection add item no 5xx');
  return res;
}
