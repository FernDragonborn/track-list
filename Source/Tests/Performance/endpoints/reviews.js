import { apiPost, apiGet } from '../helpers/http.js';
import { isNotError } from '../helpers/checks.js';

export function createReview(token, mediaId) {
  const res = apiPost(
    `/media/${mediaId}/reviews`,
    {
      rating: Math.ceil(Math.random() * 5),
      content: `Навантажувальна рецензія ${Date.now()}. Це тестовий контент для перевірки продуктивності.`,
    },
    token,
    { name: 'review_create', group: 'write' }
  );
  isNotError(res, 'review create no 5xx');
  return res;
}

export function toggleLike(token, mediaId, reviewId) {
  const res = apiPost(
    `/media/${mediaId}/reviews/${reviewId}/like`,
    {},
    token,
    { name: 'review_like', group: 'write' }
  );
  isNotError(res, 'review like no 5xx');
  return res;
}

export function createComment(token, mediaId, reviewId) {
  const res = apiPost(
    `/media/${mediaId}/reviews/${reviewId}/comments`,
    {
      content: `Навантажувальний коментар ${Date.now()}`,
      parentCommentId: null,
    },
    token,
    { name: 'comment_create', group: 'write' }
  );
  isNotError(res, 'comment create no 5xx');
  return res;
}

export function getComments(mediaId, reviewId) {
  const res = apiGet(
    `/media/${mediaId}/reviews/${reviewId}/comments`,
    null,
    { name: 'comments_get', group: 'feed' }
  );
  isNotError(res, 'comments get no 5xx');
  return res;
}
