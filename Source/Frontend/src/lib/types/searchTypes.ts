export interface MediaTranslation {
	id: string;
	mediaId: string;
	languageCode?: string;
	title?: string;
	description?: string;
	status: string;
}

export interface MediaEntity {
	id: string;
	externalApiId?: string;
	type: string;
	releaseYear?: number;
	posterUrl?: string;
	seasonCount?: number;
	episodeCount?: number;
	translations: MediaTranslation[];
}

export interface ReviewItem {
	id: string;
	mediaId: string;
	userId: string;
	username: string;
	profilePicUrl?: string;
	rating: number;
	content?: string;
	createdAt: string;
	likeCount: number;
	commentCount: number;
	isLikedByMe: boolean;
	isFromFollowing?: boolean;
}

export interface MediaDetails extends MediaEntity {
	reviews: ReviewItem[];
}

export interface SearchResult {
	id: string;
	title: string;
	year?: number;
	posterUrl?: string;
	url: string;
}
