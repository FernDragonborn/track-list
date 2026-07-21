/**
 * Stress test: ramp 10 → 50 → 100 → 200 → 0 VUs over 10 minutes.
 * Finds the breaking point — where latency degrades and errors spike.
 */
import { sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

import { withDefaults } from '../config/base.js';
import { stressThresholds } from '../config/thresholds.js';
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
    stress_ramp: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '2m', target: 50 },   // ramp up
        { duration: '3m', target: 100 },   // push harder
        { duration: '3m', target: 200 },   // stress peak
        { duration: '2m', target: 0 },     // cool-down
      ],
      exec: 'mixedTraffic',
    },
  },
  thresholds: stressThresholds,
});

export function setup() {
  return setupAuth();
}

export function mixedTraffic(data) {
  const mediaIds = seedData.mediaIds;
  const reviews = seedData.reviews;
  const page = Math.ceil(Math.random() * 5);

  // 70% anonymous, 20% authenticated, 10% admin (weighted random)
  const roll = Math.random();

  if (roll < 0.7) {
    // Anonymous browsing
    globalFeed(page);
    sleep(0.3);

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
  } else if (roll < 0.9) {
    // Authenticated actions
    const token = data.regular.accessToken;

    personalFeed(token, page);
    sleep(0.3);

    if (reviews.length > 0) {
      const r = reviews[Math.floor(Math.random() * reviews.length)];
      toggleLike(token, r.mediaId, r.reviewId);
      sleep(0.2);
      createComment(token, r.mediaId, r.reviewId);
    }
    sleep(0.3);

    if (mediaIds.length > 0) {
      upsertTracking(token, mediaIds[Math.floor(Math.random() * mediaIds.length)]);
    }
  } else {
    // Admin operations
    const adminToken = data.admin.accessToken;
    getStats(adminToken);
    sleep(0.5);

    // Skip CSV export in stress — too heavy at 200 VUs
    // exportUsersCsv(adminToken);
  }

  sleep(0.5);
}

export function handleSummary(data) {
  return {
    'results/stress_report.html': htmlReport(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
