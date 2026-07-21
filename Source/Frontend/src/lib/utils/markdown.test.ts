import { describe, it, expect } from 'vitest';
import { renderMarkdown } from './markdown';

describe('renderMarkdown — formatting', () => {
	it('returns empty string for empty input', () => {
		expect(renderMarkdown('')).toBe('');
	});

	it('renders bold and italic', () => {
		const out = renderMarkdown('**bold** and *italic*');
		expect(out).toMatch(/<strong>bold<\/strong>/);
		expect(out).toMatch(/<em>italic<\/em>/);
	});

	it('renders unordered list', () => {
		const out = renderMarkdown('- a\n- b\n- c');
		expect(out).toMatch(/<ul>/);
		expect(out).toMatch(/<li>a<\/li>/);
		expect(out).toMatch(/<li>c<\/li>/);
	});

	it('renders GFM tables', () => {
		const out = renderMarkdown('| h1 | h2 |\n|----|----|\n| a | b |');
		expect(out).toMatch(/<table>/);
		expect(out).toMatch(/<th>h1<\/th>/);
		expect(out).toMatch(/<td>a<\/td>/);
	});

	it('breaks newlines into <br> (breaks option)', () => {
		const out = renderMarkdown('line one\nline two');
		expect(out).toMatch(/<br/);
	});
});

describe('renderMarkdown — sanitization (XSS guards)', () => {
	it('strips <script> tags', () => {
		const out = renderMarkdown('hi <script>alert(1)</script>');
		expect(out).not.toMatch(/<script/i);
		expect(out).not.toMatch(/alert\(1\)/);
	});

	it('strips onerror attribute on <img>', () => {
		const out = renderMarkdown('<img src=x onerror="alert(1)">');
		expect(out).not.toMatch(/onerror/i);
	});

	it('strips javascript: href on <a>', () => {
		const out = renderMarkdown('[click](javascript:alert(1))');
		expect(out).not.toMatch(/javascript:/i);
	});

	it('strips inline event handlers on arbitrary tags', () => {
		const out = renderMarkdown('<div onclick="alert(1)">x</div>');
		expect(out).not.toMatch(/onclick/i);
	});

	it('strips <iframe>', () => {
		const out = renderMarkdown('<iframe src="https://evil.com"></iframe>');
		expect(out).not.toMatch(/<iframe/i);
	});
});
