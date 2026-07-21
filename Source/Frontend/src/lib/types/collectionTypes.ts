export type CollectionPrivacyLevel = 'public' | 'private';

export interface CollectionResponseDto {
	id: string;
	name: string;
	description?: string;
	privacyLevel: CollectionPrivacyLevel;
	ownerId: string;
	ownerUsername: string;
	itemCount: number;
	createdAt: string;
}

export interface CollectionItemDto {
	id: string;
	mediaId: string;
	mediaTitle?: string;
	mediaPosterUrl?: string;
	order?: number;
	createdAt: string;
}

export interface CollectionAccessDto {
	id: string;
	userId: string;
	username: string;
	createdAt: string;
}

export interface CollectionDetailResponseDto {
	id: string;
	name: string;
	description?: string;
	privacyLevel: CollectionPrivacyLevel;
	ownerId: string;
	ownerUsername: string;
	items: CollectionItemDto[];
	sharedWith: CollectionAccessDto[];
	createdAt: string;
}

export interface CreateCollectionBody {
	name: string;
	description?: string;
	privacyLevel?: CollectionPrivacyLevel;
}

export interface UpdateCollectionBody {
	name?: string;
	description?: string;
	privacyLevel?: CollectionPrivacyLevel;
}

export interface PagedCollections {
	items: CollectionResponseDto[];
	totalCount: number;
	pageNumber: number;
	pageSize: number;
}
