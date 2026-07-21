export interface AdminUserItem {
	id: string;
	username: string;
	email: string;
	role: string; // 'admin' | 'moderator' | 'user'  (camelCase enum from backend)
	displayName?: string;
	memberSinceYear?: number;
}

export interface TrackingDistributionDto {
	planned: number;
	watching: number;
	completed: number;
	dropped: number;
}

export interface PlatformStatsDto {
	totalUsers: number;
	totalMedia: number;
	totalReviews: number;
	totalComments: number;
	totalCollections: number;
	totalReports: number;
	pendingReports: number;
	pendingTranslations: number;
	newUsersInPeriod: number;
	newReviewsInPeriod: number;
	trackingDistribution: TrackingDistributionDto;
	statsFrom: string;
	statsTo: string;
	generatedAt: string;
}

export interface AdminTranslationItem {
	id: string;
	languageCode: string | null;
	title: string | null;
	description: string | null;
	status: string;
}

export interface AdminMediaItem {
	id: string;
	externalApiId: string | null;
	type: string;
	releaseYear: number | null;
	translationCount: number;
	translations: AdminTranslationItem[];
}
