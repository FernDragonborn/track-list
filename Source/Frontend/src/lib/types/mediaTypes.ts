export interface MediaDto {
	id: string;
	title: string;
	year?: number;
	description?: string;
	posterUrl?: string;
}

export type SearchResponse = MediaDto[];
