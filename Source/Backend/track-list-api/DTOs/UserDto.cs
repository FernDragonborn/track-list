namespace api.DTOs;

public class UserDto
{
    public Guid? Id { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public UserRole Role { get; set; }

    public string? Country { get; set; }

    public Gender Gender { get; set; }

    public string? ProfilePicUrl { get; set; }

    public string? DisplayName { get; set; }

    public string? Bio { get; set; }

    public IFormFile? ProfilePic { get; set; }
    
    public bool? IsFollowing { get; set; }

    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }

    public int MemberSinceYear { get; set; }
    public int ReviewsCount { get; set; }
    public int ListsCount { get; set; }

    public List<Review> Reviews { get; set; } = [];
    public List<Follow> Following { get; set; } = []; // На кого підписаний
    public List<Follow> Followers { get; set; } = []; // Хто підписаний
    public List<Playlist> Collections { get; set; } = []; // Власник
}