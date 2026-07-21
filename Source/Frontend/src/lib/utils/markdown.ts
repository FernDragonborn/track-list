import { marked } from 'marked';
import DOMPurify from 'isomorphic-dompurify';

marked.setOptions({ breaks: true, gfm: true });

/** Parse `raw` as GitHub-flavored Markdown and sanitize the resulting HTML. */
export function renderMarkdown(raw: string): string {
	if (!raw) return '';
	const html = marked.parse(raw, { async: false }) as string;
	return DOMPurify.sanitize(html);
}
