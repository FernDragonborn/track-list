using System.Security.Claims;
using api;
using api.Controllers;
using api.DTOs;
using api.Identity;
using api.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Reqnroll;
using Xunit;
using static api.DTOs.ResponseTypes;

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

[Binding]
public class ReviewSteps
{
    private readonly Mock<IReviewService> _reviewServiceMock;
    private readonly ReviewController _controller;
    private IActionResult? _lastResult;
    private bool _errorMockSet;

    private Guid _mediaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private Guid _reviewId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private Guid _commentId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    public ReviewSteps()
    {
        _reviewServiceMock = new Mock<IReviewService>();
        _controller = new ReviewController(_reviewServiceMock.Object, new Mock<ITranslationService>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ── Helpers ─────────────────────────────────────────────

    private void SetUser(string userId, string role = "User")
    {
        var claims = new List<Claim>
        {
            new("id", userId),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private void SetAnonymousUser()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    private static ReviewResponseDto MakeReviewDto(int rating = 4) => new()
    {
        Id = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Username = "testuser",
        Rating = rating,
        Content = "Test review",
        CreatedAt = DateTime.UtcNow,
        LikeCount = 0,
        CommentCount = 0,
        IsLikedByMe = false
    };

    private static CommentResponseDto MakeCommentDto() => new()
    {
        Id = Guid.NewGuid(),
        ReviewId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Username = "testuser",
        Content = "Test comment",
        CreatedAt = DateTime.UtcNow,
        LikeCount = 0,
        IsLikedByMe = false,
        Replies = []
    };

    // ── GIVEN ───────────────────────────────────────────────

    [Given(@"Користувач авторизований з ID ""([^""]*)"" та роллю ""([^""]*)""")]
    public void GivenUserAuthorizedWithRole(string userId, string role)
    {
        SetUser(userId, role);
    }

    [Given(@"Користувач авторизований з ID ""([^""]*)""")]
    public void GivenUserAuthorized(string userId)
    {
        SetUser(userId);
    }

    [Given(@"Користувач не має claim ""(.*)""")]
    public void GivenUserHasNoClaim(string claim)
    {
        SetAnonymousUser();
    }

    [Given(@"Існує медіа з ID ""(.*)""")]
    public void GivenMediaExists(string mediaId)
    {
        _mediaId = Guid.Parse(mediaId);
    }

    [Given(@"Сервіс повертає помилку ""(.*)""")]
    public void GivenServiceReturnsError(string errorMessage)
    {
        _errorMockSet = true;

        _reviewServiceMock
            .Setup(x => x.CreateReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateReviewRequest>()))
            .ReturnsAsync(Result.Fail<ReviewResponseDto>(errorMessage));

        _reviewServiceMock
            .Setup(x => x.UpdateReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateReviewRequest>()))
            .ReturnsAsync(Result.Fail(errorMessage));

        _reviewServiceMock
            .Setup(x => x.DeleteReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail(errorMessage));

        _reviewServiceMock
            .Setup(x => x.ToggleReviewLikeAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail<LikeResponseDto>(errorMessage));

        _reviewServiceMock
            .Setup(x => x.CreateCommentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateCommentRequest>()))
            .ReturnsAsync(Result.Fail<CommentResponseDto>(errorMessage));

        _reviewServiceMock
            .Setup(x => x.DeleteCommentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail(errorMessage));

        _reviewServiceMock
            .Setup(x => x.ToggleCommentLikeAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail<LikeResponseDto>(errorMessage));
    }

    [Given(@"Сервіс повертає список рецензій для медіа ""(.*)""")]
    public void GivenServiceReturnsReviewList(string mediaId)
    {
        _mediaId = Guid.Parse(mediaId);
        var paged = new PagedResponse<ReviewResponseDto>(
            [MakeReviewDto()], 1, 1, 10);
        _reviewServiceMock
            .Setup(x => x.GetReviewsForMediaAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok(paged));
    }

    [Given(@"Сервіс дозволяє оновлення рецензії ""(.*)""")]
    public void GivenServiceAllowsUpdate(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _reviewServiceMock
            .Setup(x => x.UpdateReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateReviewRequest>()))
            .ReturnsAsync(Result.Ok());
    }

    [Given(@"Сервіс дозволяє видалення рецензії ""(.*)""")]
    public void GivenServiceAllowsDeleteReview(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _reviewServiceMock
            .Setup(x => x.DeleteReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok());
    }

    [Given(@"Сервіс повертає результат вподобання \(isLiked: (true|false), likeCount: (\d+)\)")]
    public void GivenServiceReturnsLikeResult(string isLiked, int likeCount)
    {
        _reviewServiceMock
            .Setup(x => x.ToggleReviewLikeAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Ok(new LikeResponseDto { IsLiked = bool.Parse(isLiked), LikeCount = likeCount }));
    }

    [Given(@"Сервіс дозволяє створення коментаря до рецензії ""(.*)""")]
    public void GivenServiceAllowsCreateComment(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _reviewServiceMock
            .Setup(x => x.CreateCommentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateCommentRequest>()))
            .ReturnsAsync(Result.Ok(MakeCommentDto()));
    }

    [Given(@"Сервіс дозволяє створення відповіді до рецензії ""(.*)"" з батьківським коментарем ""(.*)""")]
    public void GivenServiceAllowsCreateReply(string reviewId, string parentCommentId)
    {
        _reviewId = Guid.Parse(reviewId);
        _reviewServiceMock
            .Setup(x => x.CreateCommentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateCommentRequest>()))
            .ReturnsAsync(Result.Ok(MakeCommentDto()));
    }

    [Given(@"Сервіс повертає список коментарів для рецензії ""(.*)""")]
    public void GivenServiceReturnsCommentList(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _reviewServiceMock
            .Setup(x => x.GetCommentsForReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync(Result.Ok(new List<CommentResponseDto> { MakeCommentDto() }));
    }

    [Given(@"Сервіс дозволяє видалення коментаря ""(.*)""")]
    public void GivenServiceAllowsDeleteComment(string commentId)
    {
        _commentId = Guid.Parse(commentId);
        _reviewServiceMock
            .Setup(x => x.DeleteCommentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok());
    }

    [Given(@"Сервіс повертає результат вподобання коментаря \(isLiked: (true|false), likeCount: (\d+)\)")]
    public void GivenServiceReturnsCommentLikeResult(string isLiked, int likeCount)
    {
        _reviewServiceMock
            .Setup(x => x.ToggleCommentLikeAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Ok(new LikeResponseDto { IsLiked = bool.Parse(isLiked), LikeCount = likeCount }));
    }

    // ── WHEN ────────────────────────────────────────────────

    [When(@"Користувач створює рецензію з рейтингом (\d+) та текстом ""(.*)""")]
    public async Task WhenUserCreatesReview(int rating, string content)
    {
        if (!_errorMockSet)
        {
            _reviewServiceMock
                .Setup(x => x.CreateReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateReviewRequest>()))
                .ReturnsAsync(Result.Ok(MakeReviewDto(rating)));
        }

        var request = new CreateReviewRequest { Rating = rating, Content = content };
        _lastResult = await _controller.CreateReview(_mediaId, request);
    }

    [When(@"Користувач отримує рецензії для медіа ""(.*)"" \(сторінка (\d+), розмір (\d+)\)")]
    public async Task WhenUserGetsReviews(string mediaId, int page, int size)
    {
        _mediaId = Guid.Parse(mediaId);
        _lastResult = await _controller.GetReviews(_mediaId, page, size);
    }

    [When(@"Користувач оновлює рецензію ""(.*)"" з рейтингом (\d+) та текстом ""(.*)""")]
    public async Task WhenUserUpdatesReview(string reviewId, int rating, string content)
    {
        _reviewId = Guid.Parse(reviewId);
        var request = new UpdateReviewRequest { Rating = rating, Content = content };
        _lastResult = await _controller.UpdateReview(_mediaId, _reviewId, request);
    }

    [When(@"Користувач видаляє рецензію ""(.*)""")]
    public async Task WhenUserDeletesReview(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _lastResult = await _controller.DeleteReview(_mediaId, _reviewId);
    }

    [When(@"Користувач ставить вподобання на рецензію ""(.*)""")]
    public async Task WhenUserToggleReviewLike(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _lastResult = await _controller.ToggleReviewLike(_mediaId, _reviewId);
    }

    [When(@"Користувач створює коментар ""(.*)"" до рецензії ""(.*)""")]
    public async Task WhenUserCreatesComment(string content, string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        var request = new CreateCommentRequest { Content = content };
        _lastResult = await _controller.CreateComment(_mediaId, _reviewId, request);
    }

    [When(@"Користувач створює відповідь ""(.*)"" до рецензії ""(.*)"" на коментар ""(.*)""")]
    public async Task WhenUserCreatesReply(string content, string reviewId, string parentCommentId)
    {
        _reviewId = Guid.Parse(reviewId);
        var request = new CreateCommentRequest
        {
            Content = content,
            ParentCommentId = Guid.Parse(parentCommentId)
        };
        _lastResult = await _controller.CreateComment(_mediaId, _reviewId, request);
    }

    [When(@"Користувач отримує коментарі для рецензії ""(.*)""")]
    public async Task WhenUserGetsComments(string reviewId)
    {
        _reviewId = Guid.Parse(reviewId);
        _lastResult = await _controller.GetComments(_mediaId, _reviewId);
    }

    [When(@"Користувач видаляє коментар ""(.*)"" з рецензії ""(.*)""")]
    public async Task WhenUserDeletesComment(string commentId, string reviewId)
    {
        _commentId = Guid.Parse(commentId);
        _reviewId = Guid.Parse(reviewId);
        _lastResult = await _controller.DeleteComment(_mediaId, _reviewId, _commentId);
    }

    [When(@"Користувач ставить вподобання на коментар ""(.*)"" рецензії ""(.*)""")]
    public async Task WhenUserToggleCommentLike(string commentId, string reviewId)
    {
        _commentId = Guid.Parse(commentId);
        _reviewId = Guid.Parse(reviewId);
        _lastResult = await _controller.ToggleCommentLike(_mediaId, _reviewId, _commentId);
    }

    // ── THEN ────────────────────────────────────────────────

    [Scope(Feature = "Рецензії, коментарі та вподобання")]
    [Then(@"Код відповіді становить (\d+)")]
    public void ThenResponseCodeIs(int expectedCode)
    {
        Helpers.ThenResponseCodeIs(expectedCode, _lastResult);
    }

    [Then(@"Відповідь містить рецензію з рейтингом (\d+)")]
    public void ThenResponseContainsReviewWithRating(int rating)
    {
        var okResult = Assert.IsType<OkObjectResult>(_lastResult);
        Assert.NotNull(okResult.Value);

        var dataProp = okResult.Value!.GetType().GetProperty("data");
        Assert.NotNull(dataProp);
        var dto = dataProp!.GetValue(okResult.Value) as ReviewResponseDto;
        Assert.NotNull(dto);
        Assert.Equal(rating, dto!.Rating);
    }

    [Scope(Feature = "Рецензії, коментарі та вподобання")]
    [Then(@"Відповідь містить помилку ""(.*)""")]
    public void ThenResponseContainsError(string expectedKeyword)
    {
        var badResult = Assert.IsType<BadRequestObjectResult>(_lastResult);
        Assert.NotNull(badResult.Value);

        var errorProp = badResult.Value!.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        var errorMsg = errorProp!.GetValue(badResult.Value)?.ToString() ?? "";
        Assert.Contains(expectedKeyword, errorMsg, StringComparison.OrdinalIgnoreCase);
    }

    [Then(@"Відповідь містить isLiked ""(.*)""")]
    public void ThenResponseContainsIsLiked(string isLiked)
    {
        var okResult = Assert.IsType<OkObjectResult>(_lastResult);
        Assert.NotNull(okResult.Value);

        var dataProp = okResult.Value!.GetType().GetProperty("data");
        Assert.NotNull(dataProp);
        var dto = dataProp!.GetValue(okResult.Value) as LikeResponseDto;
        Assert.NotNull(dto);
        Assert.Equal(bool.Parse(isLiked), dto!.IsLiked);
    }
}
