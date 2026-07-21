export interface ProfileDto {
	id?: string;
	username: string;
	email?: string;
	role?: string;
	displayName?: string;
	bio?: string;
	profilePicUrl?: string;
	country?: string;
	gender?: string;
	followersCount: number;
	followingCount: number;
	isFollowing: boolean;
	memberSinceYear?: number;
	reviewsCount?: number;
	listsCount?: number;
}

export interface UpdateProfileRequest {
	username?: string;
	email?: string;
	displayName?: string;
	bio?: string;
	profilePicUrl?: string;
	country?: string;
	gender?: string;
}
