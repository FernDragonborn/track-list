/**
 * Load test: 50 VUs (30 anon + 15 auth + 5 admin), 5 minutes.
 * Simulates expected traffic distribution under normal usage.
 */
import { sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

import { withDefaults } from '../config/base.js';
import { loadThresholds } from '../config/thresholds.js';
import { USERS } from '../config/test-users.js';
import { setupAuth } from '../helpers/auth.js';

import { globalFeed, personalFeed } from '../endpoints/feed.js';
import { searchMedia, getMediaById } from '../endpoints/media.js';
import { loginEndpoint } from '../endpoints/auth-endpoints.js';
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
    anonymous_browsing: {
      executor: 'constant-vus',
      vus: 30,
      duration: '5m',
      exec: 'anonymousBrowsing',
    },
    authenticated_actions: {
      executor: 'constant-vus',
      vus: 15,
      duration: '5m',
      exec: 'authenticatedActions',
    },
    admin_operations: {
      executor: 'constant-vus',
      vus: 5,
      duration: '5m',
      exec: 'adminOperations',
    },
  },
  thresholds: loadThresholds,
});

export function setup() {
  return setupAuth();
}

// 60% of traffic — anonymous browsing
export function anonymousBrowsing() {
  const mediaIds = seedData.mediaIds;
  const reviews = seedData.reviews;
  const page = Math.ceil(Math.random() * 5);

  globalFeed(page);
  sleep(0.5);

  searchMedia();
  sleep(0.3);

  if (mediaIds.length > 0) {
    getMediaById(mediaIds[Math.floor(Math.random() * mediaIds.length)]);
  }
  sleep(0.3);

  getPublicCollections(page);
  sleep(0.3);

  if (reviews.length > 0) {
    const r = reviews[Math.floor(Math.random() * reviews.length)];
    getComments(r.mediaId, r.reviewId);
  }

  sleep(1);
}

// 30% of traffic — authenticated user actions
export function authenticatedActions(data) {
  const token = data.regular.accessToken;
  const mediaIds = seedData.mediaIds;
  const reviews = seedData.reviews;

  // Login measurement
  loginEndpoint(USERS.regular.email, USERS.regular.password);
  sleep(0.5);

  personalFeed(token, Math.ceil(Math.random() * 3));
  sleep(0.5);

  // Create review (may 400 if duplicate — acceptable)
  if (mediaIds.length > 0) {
    const mediaIdx = (__VU * 1000 + __ITER) % mediaIds.length;
    createReview(token, mediaIds[mediaIdx]);
  }
  sleep(0.3);

  // Like + comment
  if (reviews.length > 0) {
    const r = reviews[Math.floor(Math.random() * reviews.length)];
    toggleLike(token, r.mediaId, r.reviewId);
    sleep(0.2);
    createComment(token, r.mediaId, r.reviewId);
  }
  sleep(0.3);

  // Tracking
  if (mediaIds.length > 0) {
    upsertTracking(token, mediaIds[Math.floor(Math.random() * mediaIds.length)]);
  }
  sleep(0.3);

  // Collection
  const collRes = createCollection(token);
  try {
    const collId = collRes.json().data?.id;
    if (collId && mediaIds.length > 0) {
      addItem(token, collId, mediaIds[Math.floor(Math.random() * mediaIds.length)]);
    }
  } catch (_) {}

  sleep(1);
}

// 10% of traffic — admin/moderator operations
export function adminOperations(data) {
  const adminToken = data.admin.accessToken;
  const modToken = data.moderator.accessToken;

  getStats(adminToken);
  sleep(1);

  exportUsersCsv(adminToken);
  sleep(1);

  getPendingTranslations(modToken);
  sleep(2);
}

export function handleSummary(data) {
  return {
    'results/load_report.html': htmlReport(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
