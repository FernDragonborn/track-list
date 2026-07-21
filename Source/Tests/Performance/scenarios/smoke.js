/**
 * Smoke test: 2 VUs, 1 minute.
 * Verifies all endpoints work under minimal load — baseline check.
 */
import { sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

import { withDefaults } from '../config/base.js';
import { smokeThresholds } from '../config/thresholds.js';
import { USERS } from '../config/test-users.js';
import { setupAuth } from '../helpers/auth.js';

import { globalFeed, personalFeed } from '../endpoints/feed.js';
import { searchMedia, getMediaById } from '../endpoints/media.js';
import { loginEndpoint, renewEndpoint } from '../endpoints/auth-endpoints.js';
import { createReview, toggleLike, createComment, getComments } from '../endpoints/reviews.js';
import { getPublicCollections, createCollection, addItem } from '../endpoints/collections.js';
import { upsertTracking } from '../endpoints/tracking.js';
import { getStats, exportUsersCsv } from '../endpoints/admin.js';
import { getPendingTranslations } from '../endpoints/moderation.js';

let seedData;
try {
  seedData = JSON.parse(open('../data/seed-data.json'));
} catch (_) {
  seedData = { mediaIds: [], reviews: [], collectionIds: [] };
}

export const options = withDefaults({
  scenarios: {
    smoke: {
      executor: 'constant-vus',
      vus: 2,
      duration: '1m',
    },
  },
  thresholds: smokeThresholds,
});

export function setup() {
  return setupAuth();
}

export default function (data) {
  const regularToken = data.regular.accessToken;
  const adminToken = data.admin.accessToken;
  const modToken = data.moderator.accessToken;

  const mediaIds = seedData.mediaIds;
  const reviews = seedData.reviews;
  const page = Math.ceil(Math.random() * 3);

  // Anonymous browsing
  globalFeed(page);
  searchMedia();
  if (mediaIds.length > 0) {
    getMediaById(mediaIds[Math.floor(Math.random() * mediaIds.length)]);
  }
  getPublicCollections(page);

  // Auth flow measurement
  loginEndpoint(USERS.regular.email, USERS.regular.password);

  // Authenticated actions
  personalFeed(regularToken, 1);

  // Review on unique media per VU+iteration to avoid duplicate constraint
  if (mediaIds.length > 0) {
    const mediaIdx = (__VU * 100 + __ITER) % mediaIds.length;
    createReview(regularToken, mediaIds[mediaIdx]);
  }

  // Like + comment on existing review
  if (reviews.length > 0) {
    const review = reviews[Math.floor(Math.random() * reviews.length)];
    toggleLike(regularToken, review.mediaId, review.reviewId);
    createComment(regularToken, review.mediaId, review.reviewId);
    getComments(review.mediaId, review.reviewId);
  }

  // Tracking
  if (mediaIds.length > 0) {
    upsertTracking(regularToken, mediaIds[Math.floor(Math.random() * mediaIds.length)]);
  }

  // Collection
  const collRes = createCollection(regularToken);
  try {
    const collId = collRes.json().data?.id;
    if (collId && mediaIds.length > 0) {
      addItem(regularToken, collId, mediaIds[0]);
    }
  } catch (_) {}

  // Admin
  getStats(adminToken);
  exportUsersCsv(adminToken);

  // Moderation
  getPendingTranslations(modToken);

  sleep(1);
}

export function handleSummary(data) {
  return {
    'results/smoke_report.html': htmlReport(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
