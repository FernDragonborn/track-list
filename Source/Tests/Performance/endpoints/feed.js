import { apiGet } from '../helpers/http.js';
import { isOk } from '../helpers/checks.js';

export function globalFeed(pageNumber = 1, pageSize = 10) {
  const res = apiGet(`/feed/global?pageNumber=${pageNumber}&pageSize=${pageSize}`, null, {
    name: 'feed_global',
    group: 'feed',
  });
  isOk(res, 'global feed 200');
  return res;
}

export function personalFeed(token, pageNumber = 1, pageSize = 10) {
  const res = apiGet(`/feed/personal?pageNumber=${pageNumber}&pageSize=${pageSize}`, token, {
    name: 'feed_personal',
    group: 'feed',
  });
  isOk(res, 'personal feed 200');
  return res;
}
