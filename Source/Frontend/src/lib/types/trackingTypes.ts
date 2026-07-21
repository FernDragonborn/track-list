export type TrackingStatusCode = 'planned' | 'watching' | 'completed' | 'dropped';

export interface TrackingStatusItem {
	id: string;
	userId: string;
	mediaId: string;
	status: TrackingStatusCode;
	progress?: number;
	createdAt: string;
	updatedAt: string;
}

export interface UpsertTrackingBody {
	mediaId: string;
	status: TrackingStatusCode;
	progress?: number;
}

export interface TrackingStats {
	planned: number;
	watching: number;
	completed: number;
	dropped: number;
}

export interface ProfileTrackingItem {
	mediaId: string;
	mediaTitle?: string;
	mediaPosterUrl?: string;
	mediaType?: string;
	mediaEpisodeCount?: number;
	status: TrackingStatusCode;
	progress?: number;
	createdAt: string;
	updatedAt: string;
}
