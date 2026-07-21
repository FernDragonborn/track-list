export type ReportableEntityType = 'Review' | 'Comment' | 'Profile' | 'Media';

export type ReportReason =
	| 'Spam'
	| 'Harassment'
	| 'HateSpeech'
	| 'Misinformation'
	| 'InappropriateContent'
	| 'Other';

export type ReportStatus = 'Pending' | 'ResolvedDeleted' | 'ResolvedDismissed';

export type TranslationStatus = 'Official' | 'Pending' | 'Approved' | 'Rejected';

export interface ReportTargetNavigation {
	username?: string;
	mediaId?: string;
	reviewId?: string;
	commentId?: string;
	authorUsername?: string;
	contentExcerpt?: string;
	rating?: number;
	displayName?: string;
	bio?: string;
	isDeleted?: boolean;
}

export interface ReportItem {
	id: string;
	targetId: string;
	targetType: ReportableEntityType;
	reason: ReportReason;
	comment: string | null;
	reporterId: string;
	status: ReportStatus;
	processedByUserId: string | null;
	createdAt: string;
	targetNavigation?: ReportTargetNavigation;
}

export interface CreateReportBody {
	targetId: string;
	targetType: ReportableEntityType;
	reason: ReportReason;
	comment?: string;
	reporterId?: string;
}

export interface PendingTranslationItem {
	id: string;
	mediaId: string;
	languageCode: string;
	title: string;
	description: string | null;
	status: TranslationStatus;
	createdAt: string;
	originalTitle: string | null;
	originalDescription: string | null;
}
