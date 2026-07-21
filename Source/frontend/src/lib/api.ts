import { error } from '@sveltejs/kit';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse, parseErrorMessage } from '$lib/utils/api-helpers';
import type { MediaDto, SearchResponse } from '$lib/types/mediaTypes';
import type { ProfileDto, UpdateProfileRequest } from '$lib/types/profileTypes';
import type { ReviewItem, MediaEntity } from '$lib/types/searchTypes';
import type {
	CommentItem,
	ReviewLikeResult,
	CommentLikeResult,
	CreateReviewBody,
	UpdateReviewBody,
	CreateCommentBody,
	PagedResponse,
	FeedItemDto,
	ProfileReviewItem,
} from '$lib/types/reviewTypes';
import type {
	TrackingStatusItem,
	UpsertTrackingBody,
	ProfileTrackingItem,
	TrackingStats,
} from '$lib/types/trackingTypes';
import type {
	CollectionResponseDto,
	CollectionDetailResponseDto,
	CollectionItemDto,
	CollectionAccessDto,
	CreateCollectionBody,
	UpdateCollectionBody,
	PagedCollections,
} from '$lib/types/collectionTypes';
import type {
	ReportItem,
	CreateReportBody,
	ReportStatus,
	PendingTranslationItem,
} from '$lib/types/moderationTypes';
import type { AdminUserItem, AdminMediaItem, PlatformStatsDto } from '$lib/types/adminTypes';
import type { GenreOption } from '$lib/types/genreTypes';
import type {
	ExternalContent,
	MediaRatingsBatchEntry,
	ExternalReviewerProfile,
	ExternalReviewWithMedia,
	ExternalReviewFeedItem,
	CursorPagedResult,
} from '$lib/types/externalTypes';

/** Build URL query string from object, dropping null/undefined values. */
function qs(params: Record<string, string | number | null | undefined>): string {
	const parts: string[] = [];
	for (const [k, v] of Object.entries(params)) {
		if (v === undefined || v === null || v === '') continue;
		parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
	}
	return parts.join('&');
}


const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');

export interface SendOptions {
	method: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
	path: string;
	body?: unknown;
	token?: string;
}

async function send<T>({ method, path, body, token }: SendOptions): Promise<T> {
	const opts: RequestInit = { method, credentials: 'include' };
	opts.headers = {};

	if (token) {
		opts.headers['Authorization'] = `Bearer ${token}`;
	}

	if (body !== undefined) {
		opts.headers['Content-Type'] = 'application/json';
		opts.body = JSON.stringify(body);
	}
	const url = buildApiUrl(BASE_URL, path);
	try {
		const res = await fetch(url, opts);

		if (res.status === 401) {
			throw error(401, 'Unauthorized');
		}

		if (!res.ok) {
			const errorText = await res.text();
			const errMessage = parseErrorMessage(errorText);
			throw error(res.status, errMessage);
		}

		if (res.status === 204) return {} as T;

		const responseData = await res.json();
		return unwrapResponse<T>(responseData);
	} catch (e) {
		console.error(`API Error [${method} ${path}]:`, e);
		throw e;
	}
}

async function sendFormData<T>(path: string, formData: FormData, token?: string): Promise<T> {
	const opts: RequestInit = { method: 'POST', body: formData, credentials: 'include' };
	opts.headers = {};
	if (token) {
		opts.headers['Authorization'] = `Bearer ${token}`;
	}
	const url = buildApiUrl(BASE_URL, path);
	try {
		const res = await fetch(url, opts);
		if (res.status === 401) throw error(401, 'Unauthorized');
		if (!res.ok) {
			const errorText = await res.text();
			throw error(res.status, parseErrorMessage(errorText));
		}
		if (res.status === 204) return {} as T;
		const responseData = await res.json();
		return unwrapResponse<T>(responseData);
	} catch (e) {
		console.error(`API Error [POST FormData ${path}]:`, e);
		throw e;
	}
}

export const api = {
	get: <T>	(path: string, token?: string) =>
		send<T>({ method: 'GET', path, token}),
	post: <T>	(path: string, body: unknown, token?: string) =>
		send<T>({ method: 'POST', path, body, token }),
	postFormData: <T>(path: string, formData: FormData, token?: string) =>
		sendFormData<T>(path, formData, token),
	put: <T>	(path: string, body: unknown, token?: string) =>
		send<T>({ method: 'PUT', path, body, token}),
	delete: <T>	(path: string, token?: string) =>
		send<T>({ method: 'DELETE', path, token }),

	search: (query: string): Promise<SearchResponse> =>
		send<SearchResponse>({ method: 'GET', path: `media/search?query=${encodeURIComponent(query)}` }),

	getExternalFeed: (cursor?: string, limit = 20) =>
		send<CursorPagedResult<ExternalReviewFeedItem>>({
			method: 'GET',
			path: `external-feed?${qs({ cursor, limit })}`,
		}),

	getExternalReviewerProfile: (handle: string, source = 'letterboxd', recent = 10) =>
		send<ExternalReviewerProfile>({
			method: 'GET',
			path: `external-reviewers/${encodeURIComponent(handle)}?${qs({ source, recent })}`,
		}),

	getExternalReviewerReviews: (
		handle: string,
		source = 'letterboxd',
		cursor?: string,
		limit = 20,
	) =>
		send<CursorPagedResult<ExternalReviewWithMedia>>({
			method: 'GET',
			path: `external-reviewers/${encodeURIComponent(handle)}/reviews?${qs({ source, cursor, limit })}`,
		}),

	getCatalog: async (
		page = 1,
		size = 20,
		type?: string,
		yearFrom?: number,
		yearTo?: number,
		sortBy?: string,
		genres?: number[],
	): Promise<PagedResponse<MediaEntity>> => {
		const params = new URLSearchParams({ pageNumber: String(page), pageSize: String(size) });
		if (type) params.set('type', type);
		if (yearFrom) params.set('yearFrom', String(yearFrom));
		if (yearTo) params.set('yearTo', String(yearTo));
		if (sortBy) params.set('sortBy', sortBy);
		if (genres && genres.length > 0) params.set('genres', genres.join(','));
		return send<PagedResponse<MediaEntity>>({
			method: 'GET',
			path: `media/catalog?${params.toString()}`,
		});
	},

	getGenres: (type: string): Promise<GenreOption[]> =>
		send<GenreOption[]>({ method: 'GET', path: `media/genres?type=${encodeURIComponent(type)}` }),

	getMedia: (id: string): Promise<MediaDto> =>
		send<MediaDto>({ method: 'GET', path: `media/${encodeURIComponent(id)}` }),

	getExternalContent: (mediaId: string): Promise<ExternalContent> =>
		send<ExternalContent>({ method: 'GET', path: `media/${encodeURIComponent(mediaId)}/external` }),

	getExternalRatingsBatch: (mediaIds: string[]): Promise<Record<string, ExternalContent['ratings']>> => {
		if (mediaIds.length === 0) return Promise.resolve({});
		return send<Record<string, ExternalContent['ratings']>>({
			method: 'GET',
			path: `media/external/ratings-batch?ids=${mediaIds.join(',')}`,
		});
	},

	getMediaRatingsBatch: (mediaIds: string[]): Promise<Record<string, MediaRatingsBatchEntry>> => {
		if (mediaIds.length === 0) return Promise.resolve({});
		return send<Record<string, MediaRatingsBatchEntry>>({
			method: 'GET',
			path: `media/ratings-batch?ids=${mediaIds.join(',')}`,
		});
	},

	translateExternalReview: (reviewId: string, lang: string): Promise<{ translation: string; lang: string }> =>
		send<{ translation: string; lang: string }>({
			method: 'GET',
			path: `media/external-reviews/${encodeURIComponent(reviewId)}/translate?lang=${encodeURIComponent(lang)}`,
		}),

	translateDescription: (mediaId: string, lang: string): Promise<{ translation: string; lang: string }> =>
		send<{ translation: string; lang: string }>({
			method: 'GET',
			path: `media/${encodeURIComponent(mediaId)}/description/translate?lang=${encodeURIComponent(lang)}`,
		}),

	translateReview: (mediaId: string, reviewId: string, lang: string): Promise<{ translation: string; lang: string }> =>
		send<{ translation: string; lang: string }>({
			method: 'GET',
			path: `media/${encodeURIComponent(mediaId)}/reviews/${encodeURIComponent(reviewId)}/translate?lang=${encodeURIComponent(lang)}`,
		}),

	translateComment: (mediaId: string, reviewId: string, commentId: string, lang: string): Promise<{ translation: string; lang: string }> =>
		send<{ translation: string; lang: string }>({
			method: 'GET',
			path: `media/${encodeURIComponent(mediaId)}/reviews/${encodeURIComponent(reviewId)}/comments/${encodeURIComponent(commentId)}/translate?lang=${encodeURIComponent(lang)}`,
		}),

	getProfileReviews: (
		username: string,
		pageNumber = 1,
		pageSize = 10,
		token?: string,
	): Promise<PagedResponse<ProfileReviewItem>> =>
		send<PagedResponse<ProfileReviewItem>>({
			method: 'GET',
			path: `profiles/${encodeURIComponent(username)}/reviews?pageNumber=${pageNumber}&pageSize=${pageSize}`,
			token,
		}),

	getProfile: (username: string, token?: string): Promise<ProfileDto> =>
		send<ProfileDto>({ method: 'GET', path: `profiles/${encodeURIComponent(username)}`, token }),

	updateMyProfile: (body: UpdateProfileRequest, token?: string): Promise<ProfileDto> =>
		send<ProfileDto>({ method: 'PUT', path: 'profiles/me', body, token }),

	followUser: (username: string, token?: string): Promise<void> =>
		send<void>({ method: 'POST', path: `profiles/${encodeURIComponent(username)}/follow`, token }),

	unfollowUser: (username: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `profiles/${encodeURIComponent(username)}/follow`, token }),

	getFollowers: (username: string, token?: string): Promise<ProfileDto[]> =>
		send<ProfileDto[]>({ method: 'GET', path: `profiles/${encodeURIComponent(username)}/followers`, token }),

	getFollowing: (username: string, token?: string): Promise<ProfileDto[]> =>
		send<ProfileDto[]>({ method: 'GET', path: `profiles/${encodeURIComponent(username)}/following`, token }),

	// ── Reviews ─────────────────────────────────────────────────────────────

	getReviews: async (mediaId: string, page = 1, size = 10, token?: string): Promise<ReviewItem[]> => {
		const paged = await send<PagedResponse<ReviewItem>>({ method: 'GET', path: `media/${mediaId}/reviews?pageNumber=${page}&pageSize=${size}`, token });
		return paged?.items ?? [];
	},

	createReview: (mediaId: string, body: CreateReviewBody, token?: string): Promise<ReviewItem> =>
		send<ReviewItem>({ method: 'POST', path: `media/${mediaId}/reviews`, body, token }),

	updateReview: (mediaId: string, reviewId: string, body: UpdateReviewBody, token?: string): Promise<void> =>
		send<void>({ method: 'PUT', path: `media/${mediaId}/reviews/${reviewId}`, body, token }),

	deleteReview: (mediaId: string, reviewId: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `media/${mediaId}/reviews/${reviewId}`, token }),

	toggleReviewLike: (mediaId: string, reviewId: string, token?: string): Promise<ReviewLikeResult> =>
		send<ReviewLikeResult>({ method: 'POST', path: `media/${mediaId}/reviews/${reviewId}/like`, token }),

	// ── Comments ─────────────────────────────────────────────────────────────

	getComments: (mediaId: string, reviewId: string, token?: string): Promise<CommentItem[]> =>
		send<CommentItem[]>({ method: 'GET', path: `media/${mediaId}/reviews/${reviewId}/comments`, token }),

	createComment: (mediaId: string, reviewId: string, body: CreateCommentBody, token?: string): Promise<CommentItem> =>
		send<CommentItem>({ method: 'POST', path: `media/${mediaId}/reviews/${reviewId}/comments`, body, token }),

	deleteComment: (mediaId: string, reviewId: string, commentId: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `media/${mediaId}/reviews/${reviewId}/comments/${commentId}`, token }),

	toggleCommentLike: (mediaId: string, reviewId: string, commentId: string, token?: string): Promise<CommentLikeResult> =>
		send<CommentLikeResult>({ method: 'POST', path: `media/${mediaId}/reviews/${reviewId}/comments/${commentId}/like`, token }),

	// ── Feed ─────────────────────────────────────────────────────────────────

	getPersonalFeed: async (page = 1, size = 10, token?: string, showShort = true): Promise<{ items: FeedItemDto[]; totalCount: number }> => {
		const paged = await send<PagedResponse<FeedItemDto>>({ method: 'GET', path: `feed/personal?pageNumber=${page}&pageSize=${size}&showShort=${showShort}`, token });
		return { items: paged?.items ?? [], totalCount: paged?.totalCount ?? 0 };
	},

	getGlobalFeed: async (page = 1, size = 10, token?: string): Promise<{ items: FeedItemDto[]; totalCount: number }> => {
		const paged = await send<PagedResponse<FeedItemDto>>({ method: 'GET', path: `feed/global?pageNumber=${page}&pageSize=${size}`, token });
		return { items: paged?.items ?? [], totalCount: paged?.totalCount ?? 0 };
	},

	getMyReviews: async (page = 1, size = 10, token?: string, sortBy?: string): Promise<{ items: FeedItemDto[]; totalCount: number }> => {
		const sort = sortBy ? `&sortBy=${encodeURIComponent(sortBy)}` : '';
		const paged = await send<PagedResponse<FeedItemDto>>({ method: 'GET', path: `feed/my?pageNumber=${page}&pageSize=${size}${sort}`, token });
		return { items: paged?.items ?? [], totalCount: paged?.totalCount ?? 0 };
	},

	// ── Tracking ─────────────────────────────────────────────────────────────────

	getMyTrackingStatus: (mediaId: string, token?: string): Promise<TrackingStatusItem | null> =>
		send<TrackingStatusItem | null>({ method: 'GET', path: `trackingstatus/${encodeURIComponent(mediaId)}`, token }),

	upsertTrackingStatus: (body: UpsertTrackingBody, token?: string): Promise<TrackingStatusItem> =>
		send<TrackingStatusItem>({ method: 'POST', path: 'trackingstatus', body, token }),

	deleteTrackingStatus: (mediaId: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `trackingstatus/${encodeURIComponent(mediaId)}`, token }),

	getProfileTracking: (username: string, token?: string): Promise<ProfileTrackingItem[]> =>
		send<ProfileTrackingItem[]>({ method: 'GET', path: `profiles/${encodeURIComponent(username)}/tracking`, token }),

	getMediaTrackingStats: (mediaId: string): Promise<TrackingStats> =>
		send<TrackingStats>({ method: 'GET', path: `trackingstatus/media/${encodeURIComponent(mediaId)}/stats` }),

	// ── Collections ──────────────────────────────────────────────────────────

	getUserCollections: (ownerUserId: string, token?: string, page = 1, size = 20): Promise<PagedCollections> =>
		send<PagedCollections>({ method: 'GET', path: `collections/user/${encodeURIComponent(ownerUserId)}?pageNumber=${page}&pageSize=${size}`, token }),

	getCollectionsContainingMedia: (mediaId: string): Promise<CollectionResponseDto[]> =>
		send<CollectionResponseDto[]>({ method: 'GET', path: `collections/containing/${encodeURIComponent(mediaId)}` }),

	getUserMembershipsForMedia: (mediaId: string, token: string): Promise<{ collectionId: string; itemId: string }[]> =>
		send<{ collectionId: string; itemId: string }[]>({ method: 'GET', path: `collections/memberships/${encodeURIComponent(mediaId)}`, token }),

	getCollectionDetail: (id: string, token?: string): Promise<CollectionDetailResponseDto> =>
		send<CollectionDetailResponseDto>({ method: 'GET', path: `collections/${encodeURIComponent(id)}`, token }),

	createCollection: (body: CreateCollectionBody, token?: string): Promise<CollectionResponseDto> =>
		send<CollectionResponseDto>({ method: 'POST', path: 'collections', body, token }),

	updateCollection: (id: string, body: UpdateCollectionBody, token?: string): Promise<void> =>
		send<void>({ method: 'PUT', path: `collections/${encodeURIComponent(id)}`, body, token }),

	deleteCollection: (id: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `collections/${encodeURIComponent(id)}`, token }),

	addCollectionItem: (collectionId: string, mediaId: string, token?: string): Promise<CollectionItemDto> =>
		send<CollectionItemDto>({ method: 'POST', path: `collections/${encodeURIComponent(collectionId)}/items`, body: { mediaId }, token }),

	removeCollectionItem: (collectionId: string, itemId: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `collections/${encodeURIComponent(collectionId)}/items/${encodeURIComponent(itemId)}`, token }),

	grantCollectionAccess: (collectionId: string, userId: string, token?: string): Promise<CollectionAccessDto> =>
		send<CollectionAccessDto>({ method: 'POST', path: `collections/${encodeURIComponent(collectionId)}/access`, body: { userId }, token }),

	revokeCollectionAccess: (collectionId: string, targetUserId: string, token?: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `collections/${encodeURIComponent(collectionId)}/access/${encodeURIComponent(targetUserId)}`, token }),

	// ── Moderation ───────────────────────────────────────────────────────────

	createReport: (body: CreateReportBody, token: string): Promise<ReportItem> =>
		send<ReportItem>({ method: 'POST', path: 'report', body, token }),

	getReports: (token: string, status?: ReportStatus): Promise<ReportItem[]> =>
		send<ReportItem[]>({ method: 'GET', path: `report${status ? `?reportStatus=${status}` : ''}`, token }),

	resolveReport: (id: string, resolution: 'ResolvedDeleted' | 'ResolvedDismissed', token: string): Promise<void> =>
		send<void>({ method: 'POST', path: `report/${encodeURIComponent(id)}/resolve`, body: { resolution }, token }),

	getPendingTranslations: (token: string, page = 1, size = 20): Promise<PagedResponse<PendingTranslationItem>> =>
		send<PagedResponse<PendingTranslationItem>>({ method: 'GET', path: `moderation/translations?pageNumber=${page}&pageSize=${size}`, token }),

	updateTranslationStatus: (id: string, status: 'Approved' | 'Rejected', token: string): Promise<void> =>
		send<void>({ method: 'POST', path: `moderation/translations/${encodeURIComponent(id)}/status`, body: { status }, token }),

	suggestTranslation: (mediaId: string, body: { languageCode: string; title: string; description?: string }, token: string): Promise<void> =>
		send<void>({ method: 'POST', path: `media/${encodeURIComponent(mediaId)}/translations/suggest`, body, token }),

	// ── Admin ────────────────────────────────────────────────────────────────

	getAdminUsers: (token: string, page = 1, size = 20, search?: string, sortBy = 'createdAt', sortDesc = true): Promise<PagedResponse<AdminUserItem>> => {
		const q = search ? `&searchTerm=${encodeURIComponent(search)}` : '';
		const s = `&sortBy=${sortBy}&sortDesc=${sortDesc}`;
		return send<PagedResponse<AdminUserItem>>({ method: 'GET', path: `profiles?pageNumber=${page}&pageSize=${size}${q}${s}`, token });
	},

	updateUserRole: (username: string, role: string, token: string): Promise<void> =>
		send<void>({ method: 'PUT', path: 'profiles/updateRole', body: { targetUsername: username, newRole: role }, token }),

	deleteAdminUser: (username: string, token: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `profiles/${encodeURIComponent(username)}`, token }),

	getAdminStats: (token: string, startDate?: Date, endDate?: Date): Promise<PlatformStatsDto> => {
		const params = new URLSearchParams();
		if (startDate) params.set('startDate', startDate.toISOString());
		if (endDate)   params.set('endDate',   endDate.toISOString());
		const qs = params.size ? `?${params}` : '';
		return send<PlatformStatsDto>({ method: 'GET', path: `admin/stats${qs}`, token });
	},

	getAdminMedia: (token: string, page = 1, size = 20, search?: string): Promise<PagedResponse<AdminMediaItem>> => {
		const q = search ? `&searchTerm=${encodeURIComponent(search)}` : '';
		return send<PagedResponse<AdminMediaItem>>({ method: 'GET', path: `admin/media?pageNumber=${page}&pageSize=${size}${q}`, token });
	},

	updateAdminTranslation: (id: string, body: { title: string | null; description: string | null }, token: string): Promise<void> =>
		send<void>({ method: 'PUT', path: `admin/translations/${encodeURIComponent(id)}`, body, token }),

	deleteAdminMedia: (id: string, token: string): Promise<void> =>
		send<void>({ method: 'DELETE', path: `admin/media/${encodeURIComponent(id)}`, token }),
};
