export function mapUserFromClaims(decoded: Record<string, unknown>) {
	return {
		email: String(
			decoded.email ||
			decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
			'',
		),
		role: String(
			decoded.role ||
			decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
			'User',
		),
		username: String(
			decoded.unique_name ||
			decoded.nameid ||
			decoded.name ||
			decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
			'User',
		),
		id: (decoded.id ?? decoded.sub) as string | undefined,
	};
}
