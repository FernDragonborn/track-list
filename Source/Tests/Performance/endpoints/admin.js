import { apiGet } from '../helpers/http.js';
import { isOk } from '../helpers/checks.js';

export function getStats(adminToken) {
  const res = apiGet('/admin/stats', adminToken, {
    name: 'admin_stats',
    group: 'admin',
  });
  isOk(res, 'admin stats 200');
  return res;
}

export function exportUsersCsv(adminToken) {
  const res = apiGet('/admin/export/users.csv', adminToken, {
    name: 'admin_export_csv',
    group: 'admin_export',
  });
  isOk(res, 'admin export csv 200');
  return res;
}
