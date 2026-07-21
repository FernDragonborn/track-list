/**
 * Check if user has moderator-level privileges.
 */
export function canModerate(userRole: string | null): boolean {
	return userRole === 'Admin' || userRole === 'Moderator';
}

/**
 * Check if current user owns the content.
 */
export function isOwner(contentUsername: string, currentUsername: string | null): boolean {
	return currentUsername !== null && contentUsername === currentUsername;
}
