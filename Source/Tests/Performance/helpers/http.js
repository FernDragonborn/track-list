import http from 'k6/http';
import { API_URL, DEFAULT_HEADERS } from '../config/base.js';

// GET with optional auth and tags
export function apiGet(path, token, tags = {}) {
  const params = {
    headers: token
      ? { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }
      : DEFAULT_HEADERS,
    tags,
  };
  return http.get(`${API_URL}${path}`, params);
}

// POST with JSON body, optional auth and tags
export function apiPost(path, body, token, tags = {}) {
  const params = {
    headers: token
      ? { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }
      : DEFAULT_HEADERS,
    tags,
  };
  return http.post(`${API_URL}${path}`, JSON.stringify(body), params);
}

// PUT with JSON body, optional auth and tags
export function apiPut(path, body, token, tags = {}) {
  const params = {
    headers: token
      ? { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }
      : DEFAULT_HEADERS,
    tags,
  };
  return http.put(`${API_URL}${path}`, JSON.stringify(body), params);
}

// DELETE with optional auth and tags
export function apiDelete(path, token, tags = {}) {
  const params = {
    headers: token
      ? { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }
      : DEFAULT_HEADERS,
    tags,
  };
  return http.del(`${API_URL}${path}`, null, params);
}
