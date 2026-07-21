export type ExternalRatingSource = 'imdb' | 'rotten_tomatoes' | 'metacritic';

export interface ExternalRating {
	source: ExternalRatingSource | string;
	score: number;
	rawScore?: string | null;
	voteCount?: number | null;
	fetchedAt: string;
}

export interface WikiReception {
	id: string;
	content: string;
	sourceUrl?: string | null;
	fetchedAt: string;
	/** lang code → translated text, populated when cached. */
	translations?: Record<string, string> | null;
}

export type ExternalReviewSource = 'letterboxd' | 'wikipedia_reception' | string;

export interface ExternalReview {
	id: string;
	source: ExternalReviewSource;
	authorHandle?: string | null;
	authorUrl?: string | null;
	content: string;
	rating?: number | null;
	likeCountOnSource?: number | null;
	sourceUrl?: string | null;
	publishedAt?: string | null;
	fetchedAt: string;
	/** lang code → translated text, populated when cached. */
	translations?: Record<string, string> | null;
	/** Virtual-profile metadata for the critic — avatar, displayName, link-back. */
	reviewer?: ExternalReviewer | null;
}

export type ExternalContentStatus = 'ready' | 'loading' | 'error';

export interface ExternalContent {
	status: ExternalContentStatus;
	ratings: ExternalRating[];
	wikiReception: WikiReception | null;
	reviews: ExternalReview[];
	/** lang code → translated description, populated when cached. */
	descriptionTranslations?: Record<string, string> | null;
	lastFetchedAt?: string | null;
	nextFetchDueAt?: string | null;
	lastError?: string | null;
}

export interface MediaRatingsBatchEntry {
	ourAvg: number | null;
	ourCount: number;
	external: ExternalRating[];
}

export interface ExternalReviewer {
	id: string;
	source: ExternalReviewSource;
	handle: string;
	displayName?: string | null;
	bio?: string | null;
	avatarUrl?: string | null;
	sourceProfileUrl?: string | null;
	lastSyncedAt?: string | null;
}

export interface ExternalReviewWithMedia {
	id: string;
	mediaId: string;
	mediaTitle?: string | null;
	mediaReleaseYear?: number | null;
	mediaPosterUrl?: string | null;
	source: ExternalReviewSource;
	authorHandle?: string | null;
	authorUrl?: string | null;
	content: string;
	rating?: number | null;
	sourceUrl?: string | null;
	publishedAt?: string | null;
	fetchedAt: string;
}

export interface ExternalReviewerProfile extends ExternalReviewer {
	reviewCount: number;
	averageRating?: number | null;
	recentReviews: ExternalReviewWithMedia[];
}

export interface ExternalReviewFeedItem extends ExternalReviewWithMedia {
	reviewer?: ExternalReviewer | null;
}

export interface CursorPagedResult<T> {
	items: T[];
	nextCursor?: string | null;
}
