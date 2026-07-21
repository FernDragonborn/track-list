import { writable } from 'svelte/store';
import { browser } from '$app/environment';

const STORAGE_KEY = 'preferredLang';
const DEFAULT_LANG = 'uk';

const initial: string = browser
	? localStorage.getItem(STORAGE_KEY) ?? DEFAULT_LANG
	: DEFAULT_LANG;

export const selectedLang = writable<string>(initial);

if (browser) {
	selectedLang.subscribe((v) => {
		try {
			localStorage.setItem(STORAGE_KEY, v);
		} catch {
			// ignore quota / privacy mode
		}
	});
}

export const SUPPORTED_LANGS: { code: string; label: string }[] = [
	{ code: 'uk', label: 'Українська' },
	{ code: 'en', label: 'English' },
];
