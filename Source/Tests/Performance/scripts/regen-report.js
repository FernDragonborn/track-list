#!/usr/bin/env node
/**
 * Regenerate k6 HTML report from a (patched) summary JSON.
 * Uses the same k6-reporter library as the test scenarios.
 *
 * Usage: node regen-report.js <summary.json> <output.html>
 *
 * Note: k6-reporter is a k6 JS bundle, not a Node module.
 * Instead, we build a minimal HTML report from summary JSON directly.
 */

import { readFileSync, writeFileSync } from 'fs';

const [summaryFile, outputFile] = process.argv.slice(2);
if (!summaryFile || !outputFile) {
  console.error('Usage: node regen-report.js <summary.json> <output.html>');
  process.exit(1);
}

const summary = JSON.parse(readFileSync(summaryFile, 'utf8'));
const metrics = summary.metrics || {};

function fmt(v) {
  if (v == null || v === undefined) return '—';
  if (v === 0) return '0ms';
  if (v < 1) return `${(v * 1000).toFixed(0)}µs`;
  if (v < 1000) return `${v.toFixed(2)}ms`;
  return `${(v / 1000).toFixed(2)}s`;
}

function pct(v) {
  if (v == null) return '—';
  return `${(v * 100).toFixed(2)}%`;
}

// Collect trend metrics
const trendRows = [];
for (const [name, m] of Object.entries(metrics)) {
  if (m.type !== 'trend' || !m.values) continue;
  if (name.includes('{')) continue; // skip sub-metrics
  const v = m.values;
  trendRows.push({
    name,
    avg: fmt(v.avg), min: fmt(v.min), med: fmt(v.med), max: fmt(v.max),
    p90: fmt(v['p(90)']), p95: fmt(v['p(95)']),
  });
}

// Collect rate metrics
const rateRows = [];
for (const [name, m] of Object.entries(metrics)) {
  if (m.type !== 'rate' || !m.values) continue;
  if (name.includes('{')) continue;
  const v = m.values;
  rateRows.push({ name, rate: pct(v.rate), passes: v.passes, fails: v.fails });
}

// Collect counter metrics
const counterRows = [];
for (const [name, m] of Object.entries(metrics)) {
  if (m.type !== 'counter' || !m.values) continue;
  if (name.includes('{')) continue;
  counterRows.push({ name, count: m.values.count, rate: m.values.rate?.toFixed(2) });
}

// Thresholds
const thresholdRows = [];
for (const [name, m] of Object.entries(metrics)) {
  if (!m.thresholds) continue;
  for (const [expr, result] of Object.entries(m.thresholds)) {
    thresholdRows.push({ metric: name, threshold: expr, ok: result.ok });
  }
}

const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>k6 Report (Regenerated)</title>
<style>
  body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 2rem; background: #f5f5f5; color: #333; }
  h1 { color: #7b42bc; }
  h2 { border-bottom: 2px solid #7b42bc; padding-bottom: 0.3rem; margin-top: 2rem; }
  table { border-collapse: collapse; width: 100%; margin: 1rem 0; background: white; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
  th, td { padding: 8px 12px; text-align: left; border-bottom: 1px solid #eee; }
  th { background: #7b42bc; color: white; font-weight: 600; }
  tr:hover { background: #f9f5ff; }
  .pass { color: #22c55e; font-weight: bold; }
  .fail { color: #ef4444; font-weight: bold; }
  .note { background: #fff3cd; padding: 0.75rem; border-radius: 4px; margin: 1rem 0; border-left: 4px solid #ffc107; }
</style>
</head>
<body>
<h1>k6 Performance Report (Regenerated)</h1>
<div class="note">This report was regenerated from patched summary data. 18 data points with integer overflow (negative timing values) were removed.</div>

<h2>Thresholds</h2>
<table>
<tr><th>Metric</th><th>Threshold</th><th>Status</th></tr>
${thresholdRows.map(t => `<tr><td>${t.metric}</td><td><code>${t.threshold}</code></td><td class="${t.ok ? 'pass' : 'fail'}">${t.ok ? '✓ PASS' : '✗ FAIL'}</td></tr>`).join('\n')}
</table>

<h2>HTTP Timing (Trends)</h2>
<table>
<tr><th>Metric</th><th>Avg</th><th>Min</th><th>Med</th><th>Max</th><th>p(90)</th><th>p(95)</th></tr>
${trendRows.map(r => `<tr><td>${r.name}</td><td>${r.avg}</td><td>${r.min}</td><td>${r.med}</td><td>${r.max}</td><td>${r.p90}</td><td>${r.p95}</td></tr>`).join('\n')}
</table>

<h2>Rates</h2>
<table>
<tr><th>Metric</th><th>Rate</th><th>Passes</th><th>Fails</th></tr>
${rateRows.map(r => `<tr><td>${r.name}</td><td>${r.rate}</td><td>${r.passes}</td><td>${r.fails}</td></tr>`).join('\n')}
</table>

<h2>Counters</h2>
<table>
<tr><th>Metric</th><th>Count</th><th>Rate (/s)</th></tr>
${counterRows.map(r => `<tr><td>${r.name}</td><td>${r.count}</td><td>${r.rate}</td></tr>`).join('\n')}
</table>

</body>
</html>`;

writeFileSync(outputFile, html);
console.log(`Report written to ${outputFile}`);
