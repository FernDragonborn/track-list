// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		// interface Error {}
		interface Locals {
			user: {
				username: string;
				email: string;
				role: string;
				id?: string;
			} | null;
			accessToken?: string;
		}
		interface PageData {
			user?: {
				username: string;
				email: string;
				role: string;
				id?: string;
				profilePicUrl?: string;
				bio?: string;
				memberSinceYear?: number;
			} | null;
		}
		// interface PageState {}
		// interface Platform {}
	}
}

export {};
