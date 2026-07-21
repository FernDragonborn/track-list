import { apiGet } from '../helpers/http.js';
import { isOk } from '../helpers/checks.js';

const SEARCH_TERMS = ['Тест', 'Медіа', 'movie', 'series', 'game'];

export function searchMedia(query) {
  const q = query || SEARCH_TERMS[Math.floor(Math.random() * SEARCH_TERMS.length)];
  const res = apiGet(`/media/search?query=${encodeURIComponent(q)}`, null, {
    name: 'media_search',
    group: 'media',
  });
  isOk(res, 'media search 200');
  return res;
}

export function getMediaById(mediaId) {
  const res = apiGet(`/media/${mediaId}`, null, {
    name: 'media_get',
    group: 'media',
  });
  isOk(res, 'media get 200');
  return res;
}
