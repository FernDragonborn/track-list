// Test user personas for performance testing

export const USERS = {
  regular: {
    email: 'perfuser1@test.com',
    username: 'perfuser1',
    password: 'PerfTest123!',
  },
  regular2: {
    email: 'perfuser2@test.com',
    username: 'perfuser2',
    password: 'PerfTest123!',
  },
  admin: {
    email: 'perfadmin@test.com',
    username: 'perfadmin',
    password: 'PerfAdmin123!',
  },
  moderator: {
    email: 'perfmod@test.com',
    username: 'perfmod',
    password: 'PerfMod123!',
  },
};

// Generated users for realistic data volume
export const GENERATED_USERS = Array.from({ length: 20 }, (_, i) => ({
  email: `perfgen_${String(i + 1).padStart(2, '0')}@test.com`,
  username: `perfgen_${String(i + 1).padStart(2, '0')}`,
  password: 'PerfGen123!',
}));
