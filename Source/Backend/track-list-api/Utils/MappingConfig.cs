using AutoMapper;

namespace api.Utils;

public class MappingProfile : Profile
{
	public MappingProfile()
	{
		CreateMap<User, UserDto>()
			.ForMember(dest => dest.MemberSinceYear, opt => opt.MapFrom(src => src.CreatedAt.Year))
			.ReverseMap().MaxDepth(1);
		CreateMap<Report, ReportDto>().ReverseMap();

		// ReverseMap дозволяє мапити в обидві сторони: Entity <-> DTO
		CreateMap<Media, MediaDto>().ReverseMap();
		CreateMap<MediaTranslation, MediaTranslationDto>().ReverseMap();

		// Якщо потрібно мапити статус зміни
		CreateMap<MediaTranslationStatusChangeDto, MediaTranslation>();
	}
}