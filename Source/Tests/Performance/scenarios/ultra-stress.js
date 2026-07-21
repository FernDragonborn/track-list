/**
 * Ultra-stress test: ramp 50 → 200 → 500 → 1000 → 0 VUs over 15 minutes.
 * Tests system limits far beyond normal stress — expects degradation,
 * measures how gracefully the system handles extreme load.
 */
import { sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

import { withDefaults } from '../config/base.js';
import { ultraStressThresholds } from '../config/thresholds.js';
import { USERS } from '../config/test-users.js';
import { setupAuth } from '../helpers/auth.js';

import { globalFeed, personalFeed } from '../endpoints/feed.js';
import { searchMedia, getMediaById } from '../endpoints/media.js';
import { loginEndpoint } from '../endpoints/auth-endpoints.js';
import { toggleLike, createComment, getComments } from '../endpoints/reviews.js';
import { getPublicCollections } from '../endpoints/collections.js';
import { upsertTracking } from '../endpoints/tracking.js';
import { getStats } from '../endpoints/admin.js';

let seedData;
try {
  seedData = JSON.parse(open('../data/seed-data.json'));
} catch (_) {
  seedData = { mediaIds: [], reviews: [], collectionIds: [] };
}

export const options = withDefaults({
  scenarios: {
    ultra_stress_ramp: {
      executor: 'ramping-vus',
      startVUs: 50,
      stages: [
        { duration: '2m', target: 200 },    // warm-up to known-good level
        { duration: '3m', target: 500 },     // push past stress boundary
        { duration: '5m', target: 1000 },    // ultra peak — 1000 concurrent
        { duration: '3m', target: 500 },     // gradual cool-down
        { duration: '2m', target: 0 },       // drain
      ],
      exec: 'mixedTraffic',
    },
  },
  thresholds: ultraStressThresholds,
});

export function setup() {
  return setupAuth();
}

export function mixedTraffic(data) {
  const mediaIds = seedData.mediaIds;
  const reviews = seedData.reviews;
  const page = Math.ceil(Math.random() * 5);

  // 80% anonymous, 15% authenticated, 5% admin — heavier read ratio at extreme load
  const roll = Math.random();

  if (roll < 0.8) {
    // Anonymous browsing
    globalFeed(page);
    sleep(0.2);

    searchMedia();
    sleep(0.2);

    if (mediaIds.length > 0) {
      getMediaById(mediaIds[Math.floor(Math.random() * mediaIds.length)]);
    }
    sleep(0.2);

    getPublicCollections(page);
    sleep(0.2);

    if (reviews.length > 0) {
      const r = reviews[Math.floor(Math.random() * reviews.length)];
      getComments(r.mediaId, r.reviewId);
    }
  } else if (roll < 0.95) {
    // Authenticated actions
    const token = data.regular.accessToken;

    personalFeed(token, page);
    sleep(0.2);

    if (reviews.length > 0) {
      const r = reviews[Math.floor(Math.random() * reviews.length)];
      toggleLike(token, r.mediaId, r.reviewId);
      sleep(0.1);
      createComment(token, r.mediaId, r.reviewId);
    }
    sleep(0.2);

    if (mediaIds.length > 0) {
      upsertTracking(token, mediaIds[Math.floor(Math.random() * mediaIds.length)]);
    }
  } else {
    // Admin — minimal at ultra load
    const adminToken = data.admin.accessToken;
    getStats(adminToken);
  }

  sleep(0.3);
}

export function handleSummary(data) {
  return {
    'results/ultra_stress_report.html': htmlReport(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
