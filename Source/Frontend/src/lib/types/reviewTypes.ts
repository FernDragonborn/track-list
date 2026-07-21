export interface CommentItem {
	id: string;
	reviewId: string;
	userId: string;
	username: string;
	profilePicUrl?: string;
	content?: string;
	createdAt: string;
	parentCommentId?: string;
	likeCount: number;
	isLikedByMe: boolean;
	replies: CommentItem[];
}

export interface ReviewLikeResult {
	isLiked: boolean;
	likeCount: number;
}

export interface CommentLikeResult {
	isLiked: boolean;
	likeCount: number;
}

export interface CreateReviewBody {
	rating: number;
	content?: string;
}

export interface UpdateReviewBody {
	rating: number;
	content?: string;
}

export interface CreateCommentBody {
	content: string;
	parentCommentId?: string;
}

export interface FeedCommentDto {
	id: string;
	username: string;
	content?: string;
	likeCount: number;
}

export interface FeedItemDto {
	reviewId: string;
	mediaId: string;
	mediaExternalId?: string;
	mediaTitle?: string;
	mediaPosterUrl?: string;
	userId: string;
	username: string;
	profilePicUrl?: string;
	rating: number;
	content?: string;
	createdAt: string;
	likeCount: number;
	commentCount: number;
	isLikedByMe: boolean;
	topComment?: FeedCommentDto;
}

export interface PagedResponse<T> {
	items: T[];
	totalCount: number;
	pageNumber: number;
	pageSize: number;
	totalPages: number;
}

export interface ProfileReviewItem {
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
	mediaTitle?: string;
	mediaPosterUrl?: string;
	mediaExternalApiId?: string;
}
