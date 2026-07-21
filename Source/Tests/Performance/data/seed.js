/**
 * Data generation seed script for performance testing.
 * Run: k6 run data/seed.js
 *
 * Generates: 24 users, 20 media, 60 reviews, ~120 comments,
 *            ~100 likes, 10 collections, 40 tracking statuses.
 *
 * Outputs seed-data.json IDs to stdout (capture in shell script).
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';
import { API_URL, DEFAULT_HEADERS } from '../config/base.js';
import { USERS, GENERATED_USERS } from '../config/test-users.js';

export const options = {
  scenarios: {
    seed: {
      executor: 'shared-iterations',
      vus: 1,
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {},
};

const MEDIA_TYPES = ['movie', 'series', 'game', 'book'];
const TRACKING_STATUSES = ['planned', 'watching', 'completed', 'dropped'];

function post(path, body, token) {
  const headers = token
    ? { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }
    : DEFAULT_HEADERS;
  return http.post(`${API_URL}${path}`, JSON.stringify(body), { headers });
}

function get(path, token) {
  const headers = token
    ? { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }
    : DEFAULT_HEADERS;
  return http.get(`${API_URL}${path}`, { headers });
}

function registerUser(email, username, password) {
  const res = post('/profiles/register', {
    email,
    username,
    password,
    confirmPassword: password,
  });
  // 200 = success, 400 with "already exists" = idempotent
  if (res.status !== 200 && !res.body.includes('already exist')) {
    console.warn(`Register ${username}: ${res.status} ${res.body}`);
  }
  return res;
}

function loginUser(email, password) {
  const res = post('/auth/login', { email, username: '', password });
  if (res.status !== 200) {
    console.error(`Login ${email}: ${res.status} ${res.body}`);
    return null;
  }
  try {
    return res.json().data.accessToken;
  } catch (_) {
    return null;
  }
}

function extractId(res) {
  try {
    const body = res.json();
    return body.data?.id || body.data?.Id || null;
  } catch (_) {
    return null;
  }
}

// Deterministic shuffle using seed
function shuffle(arr, seed) {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    seed = (seed * 9301 + 49297) % 233280;
    const j = Math.floor((seed / 233280) * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

export default function () {
  const seedData = {
    users: [],
    mediaIds: [],
    reviews: [],
    collectionIds: [],
    timestamp: new Date().toISOString(),
  };

  // ─── Step 1: Register all users ───
  console.log('Step 1: Registering users...');
  const allUsers = [
    ...Object.values(USERS),
    ...GENERATED_USERS,
  ];

  for (const u of allUsers) {
    registerUser(u.email, u.username, u.password);
    sleep(0.05);
  }

  // Login personas (admin/mod roles set by SQL in shell script)
  const adminToken = loginUser(USERS.admin.email, USERS.admin.password);
  const user1Token = loginUser(USERS.regular.email, USERS.regular.password);
  const user2Token = loginUser(USERS.regular2.email, USERS.regular2.password);

  if (!adminToken) {
    exec.test.abort('Admin login failed — did you run SQL role promotion?');
  }

  // Login all generated users
  const genTokens = [];
  for (const u of GENERATED_USERS) {
    const token = loginUser(u.email, u.password);
    genTokens.push(token);
    seedData.users.push({
      email: u.email,
      username: u.username,
      password: u.password,
    });
    sleep(0.05);
  }

  // ─── Step 2: Follow graph ───
  console.log('Step 2: Building follow graph...');
  // perfuser1 follows perfgen_01..perfgen_10
  for (let i = 0; i < 10; i++) {
    post(`/profiles/${GENERATED_USERS[i].username}/follow`, {}, user1Token);
    sleep(0.02);
  }
  // perfuser2 follows perfgen_05..perfgen_15
  for (let i = 4; i < 15; i++) {
    post(`/profiles/${GENERATED_USERS[i].username}/follow`, {}, user2Token);
    sleep(0.02);
  }
  // perfgen_01..perfgen_05 follow perfuser1
  for (let i = 0; i < 5; i++) {
    if (genTokens[i]) {
      post(`/profiles/${USERS.regular.username}/follow`, {}, genTokens[i]);
      sleep(0.02);
    }
  }

  // ─── Step 3: Create media (admin) ───
  console.log('Step 3: Creating media...');
  for (let i = 1; i <= 20; i++) {
    const type = MEDIA_TYPES[(i - 1) % MEDIA_TYPES.length];
    const extId = `perf:${type}:${i}`;
    const res = post('/media', {
      externalApiId: extId,
      type,
      releaseYear: 2020 + (i % 6),
      posterUrl: null,
      translations: [
        {
          languageCode: 'uk',
          title: `Тест Медіа ${i} — ${type}`,
          description: 'Опис тестового медіа для навантажувального тестування.',
        },
      ],
    }, adminToken);

    const id = extractId(res);
    if (id) {
      seedData.mediaIds.push(id);
    } else if (res.status === 400 && res.body && res.body.includes('already exists')) {
      // Duplicate — will resolve IDs after loop
      console.info(`Media ${extId}: already exists, skipping`);
    } else {
      console.warn(`Media ${extId}: unexpected ${res.status} ${res.body}`);
    }
    sleep(0.05);
  }

  if (seedData.mediaIds.length === 0) {
    console.error('No media created and none found! Check admin token and media endpoint.');
    exec.test.abort('Seed failed: no media IDs');
  }
  console.log(`Created ${seedData.mediaIds.length} media items`);

  // ─── Step 4: Reviews (60 total — 3 per generated user) ───
  console.log('Step 4: Creating reviews...');
  for (let u = 0; u < GENERATED_USERS.length; u++) {
    const token = genTokens[u];
    if (!token) continue;

    // Pick 3 unique media for this user
    const shuffled = shuffle(seedData.mediaIds, u * 137);
    const picks = shuffled.slice(0, 3);

    for (const mediaId of picks) {
      const res = post(`/media/${mediaId}/reviews`, {
        rating: (u % 5) + 1,
        content: `Рецензія від ${GENERATED_USERS[u].username}. Цей контент створено для навантажувального тестування.`,
      }, token);

      if (res.status === 200 || res.status === 201) {
        const reviewId = extractId(res);
        if (reviewId) {
          seedData.reviews.push({ mediaId, reviewId });
        }
      }
      sleep(0.03);
    }
  }
  console.log(`Created ${seedData.reviews.length} reviews`);

  // ─── Step 5: Comments + Likes ───
  console.log('Step 5: Creating comments and likes...');
  for (let r = 0; r < seedData.reviews.length; r++) {
    const { mediaId, reviewId } = seedData.reviews[r];

    // 2 comments from random users
    for (let c = 0; c < 2; c++) {
      const commenterIdx = (r * 3 + c) % GENERATED_USERS.length;
      const token = genTokens[commenterIdx];
      if (!token) continue;

      post(`/media/${mediaId}/reviews/${reviewId}/comments`, {
        content: `Коментар #${c + 1} від ${GENERATED_USERS[commenterIdx].username}`,
        parentCommentId: null,
      }, token);
      sleep(0.02);
    }

    // 1-2 likes from random users
    const likerCount = (r % 2) + 1;
    for (let l = 0; l < likerCount; l++) {
      const likerIdx = (r * 7 + l + 5) % GENERATED_USERS.length;
      const token = genTokens[likerIdx];
      if (!token) continue;

      post(`/media/${mediaId}/reviews/${reviewId}/like`, {}, token);
      sleep(0.02);
    }
  }

  // ─── Step 6: Collections (10) ───
  console.log('Step 6: Creating collections...');
  for (let i = 0; i < 10; i++) {
    const token = genTokens[i];
    if (!token) continue;

    const isPublic = i < 7;
    const res = post('/collections', {
      name: `Колекція ${GENERATED_USERS[i].username}`,
      description: 'Тестова колекція для навантажувального тестування',
      privacyLevel: isPublic ? 'public' : 'private',
    }, token);

    const collId = extractId(res);
    if (collId) {
      seedData.collectionIds.push(collId);

      // Add 3-5 media items
      const itemCount = 3 + (i % 3);
      const shuffled = shuffle(seedData.mediaIds, i * 251);
      for (let j = 0; j < itemCount && j < shuffled.length; j++) {
        post(`/collections/${collId}/items`, {
          mediaId: shuffled[j],
          order: j + 1,
        }, token);
        sleep(0.02);
      }
    }
    sleep(0.03);
  }
  console.log(`Created ${seedData.collectionIds.length} collections`);

  // ─── Step 7: Tracking statuses (40) ───
  console.log('Step 7: Creating tracking statuses...');
  for (let u = 0; u < GENERATED_USERS.length; u++) {
    const token = genTokens[u];
    if (!token) continue;

    const shuffled = shuffle(seedData.mediaIds, u * 431);
    for (let t = 0; t < 2 && t < shuffled.length; t++) {
      post('/trackingstatus', {
        mediaId: shuffled[t],
        status: TRACKING_STATUSES[(u * 2 + t) % TRACKING_STATUSES.length],
      }, token);
      sleep(0.02);
    }
  }

  // ─── Output seed data ───
  console.log('=== SEED_DATA_JSON_START ===');
  console.log(JSON.stringify(seedData));
  console.log('=== SEED_DATA_JSON_END ===');
  console.log('Seed complete!');
}
