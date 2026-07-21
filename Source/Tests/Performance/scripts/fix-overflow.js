#!/usr/bin/env node
/**
 * Fix k6 JSON output: remove data points with negative values (integer overflow).
 * Then recompute summary stats and overwrite summary JSON.
 *
 * Usage: node fix-overflow.js <k6-json-output> <summary-json>
 */

import { createReadStream, writeFileSync, readFileSync } from 'fs';
import { createInterface } from 'readline';

const [jsonFile, summaryFile] = process.argv.slice(2);
if (!jsonFile || !summaryFile) {
  console.error('Usage: node fix-overflow.js <k6-output.json> <summary.json>');
  process.exit(1);
}

// Collect all trend metric data points (only affected metrics)
const TIMING_METRICS = new Set([
  'http_req_duration', 'http_req_sending', 'http_req_receiving',
  'http_req_waiting', 'http_req_blocked', 'http_req_connecting',
  'http_req_tls_handshaking',
]);

const metricPoints = {}; // metric -> [values]
let removedCount = 0;
let totalPoints = 0;

const rl = createInterface({ input: createReadStream(jsonFile), crlfDelay: Infinity });

for await (const line of rl) {
  if (!line.trim()) continue;
  let obj;
  try { obj = JSON.parse(line); } catch { continue; }

  if (obj.type !== 'Point') continue;
  const metric = obj.metric;
  if (!TIMING_METRICS.has(metric)) continue;

  totalPoints++;
  const val = obj.data?.value;
  if (typeof val === 'number' && val < 0) {
    removedCount++;
    continue;
  }
  if (!metricPoints[metric]) metricPoints[metric] = [];
  metricPoints[metric].push(val);
}

console.log(`Total timing points: ${totalPoints}, removed overflow: ${removedCount}`);

// Compute stats
function computeStats(values) {
  values.sort((a, b) => a - b);
  const n = values.length;
  const sum = values.reduce((s, v) => s + v, 0);
  const pctl = (p) => {
    const idx = Math.ceil(p / 100 * n) - 1;
    return values[Math.max(0, idx)];
  };
  return {
    avg: sum / n,
    min: values[0],
    med: pctl(50),
    max: values[n - 1],
    'p(90)': pctl(90),
    'p(95)': pctl(95),
    'p(99)': pctl(99),
    count: n,
  };
}

// Read existing summary, patch affected metrics
const summary = JSON.parse(readFileSync(summaryFile, 'utf8'));

for (const [metric, values] of Object.entries(metricPoints)) {
  if (values.length === 0) continue;
  const stats = computeStats(values);
  console.log(`${metric}: min=${stats.min.toFixed(2)}ms, avg=${stats.avg.toFixed(2)}ms, p95=${stats['p(95)'].toFixed(2)}ms, max=${stats.max.toFixed(2)}ms (n=${stats.count})`);

  // Patch summary JSON (supports both flat and nested {values: {...}} formats)
  if (summary.metrics?.[metric]) {
    const m = summary.metrics[metric];
    const v = m.values ?? m; // k6 v0.47+ uses flat, older uses .values
    v.avg = stats.avg;
    v.min = stats.min;
    v.med = stats.med;
    v.max = stats.max;
    v['p(90)'] = stats['p(90)'];
    v['p(95)'] = stats['p(95)'];
    if (v['p(99)'] !== undefined) v['p(99)'] = stats['p(99)'];
  }
}

writeFileSync(summaryFile, JSON.stringify(summary, null, 2));
console.log(`\nPatched summary written to ${summaryFile}`);
