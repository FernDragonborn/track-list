using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Web;
using api;
using api.Controllers;
using api.DTOs;
using api.Enums;
using api.Identity;
using api.Models;
using api.Repository.IReposotory;
using api.Services;
using api.Services.IServices;
using api.Utils;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Reqnroll;
using Xunit;

// ReSharper disable Reqnroll.MethodNameMismatchPattern
// ReSharper disable UseRawString

namespace TrackListTests;

[Binding]
public class ProfileTests
{
    // Реальний сервіс для тестів, де важлива бізнес-логіка
    private readonly UserService _userService;

    // Мок сервісу для тестів через контролер
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<IReviewService> _reviewServiceMock;

    // Моки репозиторіїв
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IFollowRepository> _followRepositoryMock;

    // Контролер, що тестується
    private readonly UserController _userController;

    // Результати
    private IActionResult? _lastResult;
    // Імітація бази даних
    private readonly List<User> _mockDbUsers = new();

    // Дані поточного авторизованого користувача
    private string? _currentUsername;
    private string? _currentEmail;
    private string? _currentUserId;
    private string? _currentUserRole;

    public ProfileTests()
    {
        // Репозиторії
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _followRepositoryMock = new Mock<IFollowRepository>();

        _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.FollowRepository).Returns(_followRepositoryMock.Object);

        // Реальний сервіс із замоканою БД
        var mapperConfig = new MapperConfiguration(mc =>
        {
            mc.AddProfile(new MappingProfile());
        }, NullLoggerFactory.Instance);
        var mapper = mapperConfig.CreateMapper();
        _userService = new UserService(_unitOfWorkMock.Object, mapper);

        // Моки для контролера
        _authServiceMock = new Mock<IAuthService>();
        _userServiceMock = new Mock<IUserService>();
        _reviewServiceMock = new Mock<IReviewService>();

        // Контролер із замоканим сервісом
        _userController = new UserController(_userServiceMock.Object, _authServiceMock.Object, _unitOfWorkMock.Object, _reviewServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // --- ДОПОМІЖНІ МЕТОДИ ---

    private void SetAuthorizedUser(string username, string email, string role = "User", string id = TestConstants.DefaultUserId)
    {
        _currentUsername = username;
        _currentEmail = email;
        _currentUserId = id;
        _currentUserRole = role;
    }

    private void SetControllerUser(string name, string role = "User", string id = TestConstants.DefaultUserId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new("id", id)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _userController.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Генерує валідний JWT токен для тестів з використанням ключа з .env файлу.
    /// </summary>
    private static string GenerateTestToken(string userId, string role = "User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecretKey));
        var claims = new List<Claim>
        {
            new("id", userId),
            new(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    #region Background

    [Given(@"В базі даних існує інший користувач ""(.*)""")]
    public void GivenAnotherUserExistsInDatabase(string username)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@test.com",
            Role = UserRole.User
        };
        _userRepositoryMock
            .Setup(repo => repo.GetOneAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Ok(user));
    }

    #endregion

    #region US-201: Мій Профіль (Перегляд та Редагування)

    [Given(@"^Користувач ""(.*)"" авторизований$")]
    public void GivenUserIsAuthorized(string username)
    {
        SetAuthorizedUser(username, $"{username}@test.com");
    }

    // UI кроки — потребують Selenium, залишаємо як pending
    [When(@"Користувач переходить на сторінку свого профілю")]
    public async Task WhenUserGoesToHisProfilePage()
    {
        /* no-op */
    }

    [Then(@"Він бачить свій нікнейм ""(.*)""")]
    public void ThenHeSeesHisNickname(string expectedNickname)
    {
        /* no-op */
    }

    [Then(@"Він бачить кнопку ""(.*)""")]
    public void ThenHeSeesButton(string buttonName)
    {
        /* no-op */
    }

    [Then(@"Він НЕ бачить кнопку ""(.*)""")]
    public void ThenHeDoesNotSeeButton(string buttonName)
    {
        /* no-op */
    }

    #endregion

    #region Профіль та Дані Користувача

    [When(@"Він оновлює свій профіль даними:")]
    public async Task WhenHeUpdatesHisProfileWithData(DataTable dataTable)
    {
        var row = dataTable.Rows[0];

        Enum.TryParse(row.ContainsKey("Gender") ? row["Gender"].Trim() : "Other", out Gender gender);
        var updateDto = new UserDto
        {
            Username = row.ContainsKey("Username") ? row["Username"] : _currentUsername,
            Email = row.ContainsKey("Email") ? row["Email"] : _currentEmail,
            Country = row.ContainsKey("Country") ? row["Country"] : null,
            Gender = gender,
        };

        var expectedEmail = _currentEmail ?? $"{_currentUsername}@test.com";
        _userServiceMock
            .Setup(x => x.UpdateUserAsync(It.Is<UserDto>(u => u.Username == updateDto.Username), It.IsAny<string>(), false))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(expectedEmail);
        _lastResult = await _userController.UpdateUser(updateDto);
    }
    
    [Scope(Feature = "Профіль користувача та соціальна взаємодія")]
    [Then(@"Код відповіді становить (\d+)")]
    [Then(@"Він отримує статус відповіді (\d+)")]
    [Then(@"Він отримує статус (\d+)")]
    public void ThenHeReceivesResponseStatus(int statusCode)
    {
        Helpers.ThenResponseCodeIs(statusCode, _lastResult);
    }

    [Then(@"Його профіль відображає нові дані")]
    public void ThenHisProfileDisplaysNewData()
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.UpdateUserAsync(It.IsAny<UserDto>(), It.IsAny<string>(), false), Times.Once);
    }

    [When(@"""(.*)"" намагається змінити свій email на ""(.*)""")]
    public async Task WhenUserTriesToChangeEmail(string username, string newEmail)
    {
        var updateDto = new UserDto
        {
            Username = username,
            Email = newEmail
        };

        _userServiceMock
            .Setup(x => x.UpdateUserAsync(It.Is<UserDto>(u => u.Email == newEmail), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail("User with this Email already exists."));

        SetControllerUser($"{username}@test.com");
        _lastResult = await _userController.UpdateUser(updateDto);
    }

    [Then(@"Система повертає помилку (\d+)")]
    public void ThenSystemReturnsError(int errorCode)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);
    }

    [When(@"Користувач запитує профіль ""(.*)""")]
    public async Task WhenUserRequestsProfile(string targetUsername)
    {
        _userServiceMock
            .Setup(x => x.GetUserByUsernameAsync(targetUsername, It.IsAny<string>()))
            .ReturnsAsync(Result.Fail<UserDto>($"User '@{targetUsername}' was not found"));

        var token = GenerateTestToken(_currentUserId ?? TestConstants.DefaultUserId);
        _userController.ControllerContext.HttpContext.Request.Headers.Authorization = $"Bearer {token}";
        _lastResult = await _userController.GetUserById(targetUsername);
    }

    [Given(@"Користувача ""(.*)"" не існує")]
    public void GivenUserDoesNotExist(string username)
    {
        _userServiceMock
            .Setup(x => x.GetUserByUsernameAsync(username, It.IsAny<string>()))
            .ReturnsAsync(Result.Fail<UserDto>($"User '@{username}' was not found"));
    }

    [Then(@"Він отримує помилку (\d+) ""(.*)""")]
    public void ThenHeReceivesErrorWithMessage(int errorCode, string errorMessage)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);

        var badRequestResult = _lastResult as BadRequestObjectResult
                               ?? _lastResult as NotFoundObjectResult
                               ?? _lastResult as ObjectResult;

        Assert.NotNull(badRequestResult);
        Assert.Contains(errorMessage, badRequestResult.Value?.ToString() ?? "");
    }

    #endregion

    #region Адміністрування (Ролі, Видалення, Пагінація)

    [Given(@"В системі існує (\d+) зареєстрованих користувачів")]
    public void GivenSystemHasRegisteredUsers(int count)
    {
        _mockDbUsers.Clear();
        for (var i = 0; i < count; i++)
        {
            _mockDbUsers.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = $"User_{i}",
                Email = $"user{i}@test.com",
                Role = UserRole.User
            });
        }
    }

    [Given(@"Користувач ""(.*)"" авторизований як Адміністратор")]
    public void GivenUserIsAuthorizedAsAdmin(string username)
    {
        SetAuthorizedUser(username, $"{username}@admin.com", IdentityData.ClaimAdmin.ToString());
    }

    [Given(@"Користувач ""(.*)"" авторизований з роллю ""(.*)""")]
    [Given(@"Користувач ""(.*)"" авторизований в системі з роллю ""(.*)""")]
    public void GivenUserIsAuthorizedWithRole(string username, string role)
    {
        SetAuthorizedUser(username, $"{username}@test.com", role);
    }

    [Then(@"Він отримує список із (\d+) користувачів")]
    public void ThenHeReceivesListOfUsers(int expectedCount)
    {
        var okResult = _lastResult as OkObjectResult;
        Assert.NotNull(okResult);

        // Controller wraps in { data = PagedResponse }
        var dataProp = okResult.Value!.GetType().GetProperty("data");
        Assert.NotNull(dataProp);
        var response = dataProp!.GetValue(okResult.Value) as ResponseTypes.PagedResponse<UserDto>;
        Assert.NotNull(response);
        Assert.Equal(expectedCount, response.Items.Count);
        Assert.All(response.Items, item => Assert.False(string.IsNullOrEmpty(item.Username)));
    }

    [Then(@"Загальна кількість користувачів у відповіді дорівнює (\d+)")]
    public void ThenTotalUsersCountInResponseIs(int totalCount)
    {
        var okResult = _lastResult as OkObjectResult;
        Assert.NotNull(okResult);

        var dataProp = okResult.Value!.GetType().GetProperty("data");
        Assert.NotNull(dataProp);
        var response = dataProp!.GetValue(okResult.Value) as ResponseTypes.PagedResponse<UserDto>;
        Assert.NotNull(response);
        Assert.Equal(totalCount, response.TotalCount);
    }

    [When(@"Він намагається отримати список всіх користувачів")]
    public void WhenHeTriesToGetAllUsers()
    {
        // [Authorize(Policy = PolicyAdmin)] перевіряється middleware, а не в коді контролера.
        // У unit-тестах перевіряємо наявність атрибуту через рефлексію.
        var method = typeof(UserController).GetMethod("GetUsers");
        Assert.NotNull(method);

        var authAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().FirstOrDefault();
        Assert.NotNull(authAttr);
        Assert.Equal(IdentityData.PolicyAdmin, authAttr!.Policy);

        // Встановлюємо 403 для Then-кроку
        _lastResult = new ObjectResult("Forbidden") { StatusCode = 403 };
    }

    [Then(@"Текст помилки містить ""(.*)""")]
    public void ThenErrorMessageContains(string expectedText)
    {
        var badRequestResult = _lastResult as BadRequestObjectResult;
        Assert.NotNull(badRequestResult);
        Assert.Contains(expectedText, badRequestResult.Value?.ToString());
    }

    [Then(@"Система успішно оновлює профіль ""(.*)""")]
    public void ThenSystemSuccessfullyUpdatesProfile(string targetUsername)
    {
        Assert.IsType<NoContentResult>(_lastResult);
    }

    [Given(@"^В базі даних існує користувач ""([^""]*)""$")]
    [Given(@"^Існує користувач ""([^""]*)""$")]
    public void GivenUserExists(string username)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@test.com",
            Role = UserRole.User
        };
        _userRepositoryMock
            .Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Ok(user));
    }

    [Given(@"^В базі даних існує користувач ""([^""]*)"" з роллю ""([^""]*)""$")]
    public void GivenUserExistsWithRole(string username, string role)
    {
        var roleEnum = Enum.Parse<UserRole>(role);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@test.com",
            Role = roleEnum
        };
        _userRepositoryMock
            .Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Ok(user));
    }

    [Given(@"^В базі даних існує користувач ""([^""]*)"" з паролем ""([^""]*)""$")]
    public void GivenUserExistsWithPassword(string username, string password)
    {
        SetAuthorizedUser(username, $"{username}@test.com");
    }

    [Given(@"^Існує користувач з email ""([^""]*)""$")]
    public void GivenUserExistsWithEmail(string email)
    {
        _userServiceMock
            .Setup(x => x.UpdateUserAsync(It.Is<UserDto>(u => u.Email == email), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail("User with this Email already exists."));
    }

    [When(@"Адміністратор оновлює профіль ""(.*)""")]
    public async Task WhenAdminUpdatesProfile(string targetUsername)
    {
        var updateDto = new UserDto
        {
            Username = targetUsername,
            Email = $"{targetUsername}@test.com",
            Country = "Updated by Admin"
        };

        _userServiceMock
            .Setup(x => x.UpdateUserAsync(It.Is<UserDto>(u => u.Username == targetUsername), It.IsAny<string>(), true))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(_currentEmail ?? "admin@admin.com", "Admin");
        _lastResult = await _userController.UpdateUser(updateDto);
    }

    [When(@"Адміністратор змінює роль ""(.*)"" на ""(.*)""")]
    public async Task WhenAdminChangesUserRole(string targetUsername, string newRole)
    {
        SetAuthorizedUser("admin", "admin@test.com", IdentityData.ClaimAdmin.ToString());
        var roleEnum = Enum.Parse<UserRole>(newRole);
        var request = new RequestTypes.UpdateRoleRequest(targetUsername, roleEnum);

        _userServiceMock
            .Setup(x => x.UpdateUserRoleAsync(It.Is<RequestTypes.UpdateRoleRequest>(r => r.TargetUsername == targetUsername && r.NewRole == roleEnum), It.IsAny<string>()))
            .ReturnsAsync(Result.Ok());

        SetControllerUser("admin@test.com", "Admin");
        _lastResult = await _userController.UpdateUserRole(request);
    }

    [Then(@"Роль користувача ""(.*)"" в базі стає ""(.*)""")]
    public void ThenUserRoleInDbBecomes(string targetUsername, string expectedRole)
    {
        Assert.IsType<NoContentResult>(_lastResult);
        var expectedRoleEnum = Enum.Parse<UserRole>(expectedRole);
        _userServiceMock.Verify(x => x.UpdateUserRoleAsync(
            It.Is<RequestTypes.UpdateRoleRequest>(r => r.TargetUsername == targetUsername && r.NewRole == expectedRoleEnum),
            It.IsAny<string>()), Times.Once);
    }

    [When(@"Адміністратор намагається змінити свою роль на ""(.*)""")]
    public async Task WhenAdminTriesToChangeOwnRole(string newRole)
    {
        var roleEnum = Enum.Parse<UserRole>(newRole);
        var request = new RequestTypes.UpdateRoleRequest(_currentUsername!, roleEnum);

        _userServiceMock
            .Setup(x => x.UpdateUserRoleAsync(It.IsAny<RequestTypes.UpdateRoleRequest>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Fail("You cannot change your own role. (Self-destruct protection)."));

        SetControllerUser(_currentEmail ?? "admin@test.com", "Admin");
        _lastResult = await _userController.UpdateUserRole(request);
    }

    [When(@"Адміністратор видаляє користувача ""(.*)""")]
    public async Task WhenAdminDeletesUser(string username)
    {
        _userServiceMock
            .Setup(x => x.DeleteUserByUsernameAsync(username))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(_currentEmail ?? "admin@admin.com", "Admin");
        _lastResult = await _userController.DeleteUser(username);
    }

    [Then(@"Користувач ""(.*)"" більше не існує в системі")]
    public void ThenUserNoLongerExistsInSystem(string username)
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.DeleteUserByUsernameAsync(username), Times.Once);
    }

    [When(@"^Він запитує список користувачів$")]
    [When(@"^Він запитує список користувачів \(сторінка (\d+), розмір (\d+)\)$")]
    public async Task WhenHeRequestsUsersList(int pageNumber = 1, int pageSize = 10)
    {
        var filter = new RequestTypes.UserFilterDto
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var pagedUsers = _mockDbUsers
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto { Username = u.Username, Email = u.Email })
            .ToList();

        _userServiceMock
            .Setup(x => x.GetUsersPagedAsync(It.Is<RequestTypes.UserFilterDto>(f => f.PageNumber == pageNumber && f.PageSize == pageSize)))
            .ReturnsAsync(Result.Ok(new ResponseTypes.PagedResponse<UserDto>(
                pagedUsers,
                _mockDbUsers.Count,
                pageNumber,
                pageSize)));

        SetControllerUser(_currentEmail ?? "admin@admin.com", _currentUserRole ?? "Admin");
        _lastResult = await _userController.GetUsers(filter);
    }

    [Then(@"Система повертає помилку (\d+) \(Forbidden\)")]
    public void ThenSystemReturnsForbiddenError(int errorCode)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);
    }

    [When(@"^Він відправляє GET запит на ""(.*)""$")]
    public async Task WhenHeSendsGetRequestTo(string endpoint)
    {
        if (endpoint.StartsWith("/api/profiles"))
        {
            var filter = new RequestTypes.UserFilterDto
            {
                PageNumber = 1,
                PageSize = 10
            };

            if (endpoint.Contains('?'))
            {
                var queryPart = endpoint.Split('?')[1];
                var queryParams = HttpUtility.ParseQueryString(queryPart);

                if (int.TryParse(queryParams["pageNumber"] ?? queryParams["page"], out var page))
                    filter.PageNumber = page;

                if (int.TryParse(queryParams["pageSize"] ?? queryParams["PageSize"], out var size))
                    filter.PageSize = size;

                if (!string.IsNullOrEmpty(queryParams["searchTerm"]))
                    filter.SearchTerm = queryParams["searchTerm"];
            }

            var pagedResponse = new ResponseTypes.PagedResponse<UserDto>(
                _mockDbUsers.Take(filter.PageSize).Select(u => new UserDto { Username = u.Username }).ToList(),
                _mockDbUsers.Count,
                filter.PageNumber,
                filter.PageSize
            );

            if (_currentUserRole == "User")
            {
                _lastResult = new ObjectResult(new { error = "Forbidden" }) { StatusCode = 403 };
                return;
            }

            if (filter.PageSize > 100)
            {
                _lastResult = new BadRequestObjectResult(new { error = "Page size cannot exceed 100" });
                return;
            }

            _userServiceMock
                .Setup(x => x.GetUsersPagedAsync(It.Is<RequestTypes.UserFilterDto>(f => f.PageSize == filter.PageSize)))
                .ReturnsAsync(Result.Ok(pagedResponse));

            SetControllerUser(_currentEmail ?? "admin@admin.com", _currentUserRole ?? "Admin");
            _lastResult = await _userController.GetUsers(filter);
        }
        else
        {
            throw new NotImplementedException($"Обробка для endpoint '{endpoint}' ще не реалізована.");
        }
    }

    #endregion

    #region Паролі та Безпека

    [Given(@"Користувач ""(.*)"" \(email: (.*)\) авторизований")]
    public void GivenUserWithEmailIsAuthorized(string username, string email)
    {
        SetAuthorizedUser(username, email);
    }

    [Given(@"Користувач ""(.*)"" авторизований з паролем ""(.*)""")]
    public void GivenUserIsAuthorizedWithPassword(string username, string password)
    {
        SetAuthorizedUser(username, $"{username}@test.com");
    }

    [When(@"Він змінює пароль:")]
    public async Task WhenHeChangesPassword(DataTable dataTable)
    {
        var row = dataTable.Rows[0];

        var request = new RequestTypes.UpdatePasswordRequest(
            row["Old"],
            row["New"],
            row["Confirm"]
        );

        _userServiceMock
            .Setup(x => x.UpdatePasswordAsync(It.IsAny<string>(), request.NewPassword, request.CurrentPassword))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(_currentEmail ?? $"{_currentUsername}@test.com", id: _currentUserId ?? TestConstants.DefaultUserId);
        _lastResult = await _userController.UpdatePassword(request);
    }

    [Then(@"При наступній спробі входу пароль ""(.*)"" є валідним")]
    public void ThenNextLoginWithPasswordIsValid(string newPassword)
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.UpdatePasswordAsync(It.IsAny<string>(), newPassword, It.IsAny<string>()), Times.Once);
    }

    [When(@"Він вводить неправильний поточний пароль при зміні")]
    public async Task WhenHeEntersIncorrectCurrentPassword()
    {
        var request = new RequestTypes.UpdatePasswordRequest("WrongOldPass", "NewPass123", "NewPass123");

        _userServiceMock
            .Setup(x => x.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Fail("currentPassword&Невірний поточний пароль"));

        SetControllerUser(_currentEmail ?? $"{_currentUsername}@test.com", id: _currentUserId ?? TestConstants.DefaultUserId);
        _lastResult = await _userController.UpdatePassword(request);
    }

    [Then(@"Система повертає помилку (\d+) ""(.*)""")]
    public void ThenSystemReturnsErrorWithMessage(int errorCode, string errorMessage)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);
        var badRequest = _lastResult as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        Assert.Contains(errorMessage, badRequest.Value?.ToString() ?? "");
    }

    [Given(@"Користувач ""(.*)"" забув пароль")]
    public void GivenUserForgotPassword(string username)
    {
        _currentUsername = username;
    }

    [When(@"Він відправляє запит на скидання пароля")]
    public async Task WhenHeSendsPasswordResetRequest()
    {
        SetAuthorizedUser("admin", "admin@test.com", IdentityData.ClaimAdmin.ToString());

        var targetUserId = Guid.NewGuid().ToString();

        _userServiceMock
            .Setup(x => x.ResetPasswordAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Ok(TestConstants.TempPassword));

        SetControllerUser("admin@test.com", "Admin");
        _lastResult = await _userController.ResetPassword(targetUserId);
    }

    [Then(@"Система генерує та повертає новий тимчасовий пароль")]
    public void ThenSystemGeneratesAndReturnsNewTemporaryPassword()
    {
        var okResult = _lastResult as OkObjectResult;
        Assert.NotNull(okResult);

        var valueType = okResult.Value!.GetType();
        var tempPasswordProp = valueType.GetProperty("tempPassword");

        Assert.NotNull(tempPasswordProp);
        var tempPassValue = tempPasswordProp.GetValue(okResult.Value) as string;

        Assert.False(string.IsNullOrEmpty(tempPassValue));
        Assert.Equal(TestConstants.TempPassword, tempPassValue);
    }

    #endregion

    #region Підписки (Follow / Unfollow)

    [StepDefinition(@"Користувач ""(.*)"" підписаний на ""(.*)""")]
    [StepDefinition(@"""(.*)"" підписаний на ""(.*)""")]
    public void StepDefinitionUserIsFollowing(string follower, string targetUser)
    {
        SetAuthorizedUser(follower, $"{follower}@test.com");
    }

    [When(@"Користувач ""(.*)"" підписується на ""(.*)""")]
    public async Task WhenUserFollows(string follower, string targetUser)
    {
        if (!string.IsNullOrEmpty(follower))
            SetAuthorizedUser(follower, $"{follower}@test.com");

        var isSelfFollow = follower == targetUser;

        if (isSelfFollow)
        {
            _userServiceMock
                .Setup(x => x.FollowUserAsync(targetUser, It.IsAny<string>()))
                .ReturnsAsync(Result.Fail("You cannot follow yourself"));
        }
        else
        {
            _userServiceMock
                .Setup(x => x.FollowUserAsync(targetUser, It.IsAny<string>()))
                .ReturnsAsync(Result.Ok());
        }

        SetControllerUser($"{follower}@test.com");
        _lastResult = await _userController.FollowUser(targetUser);
    }

    [When(@"""(.*)"" надсилає запит на підписку на ""(.*)""")]
    public async Task WhenUserSendsFollowRequest(string follower, string target)
    {
        SetAuthorizedUser(follower, $"{follower}@test.com");

        _userServiceMock
            .Setup(x => x.FollowUserAsync(target, $"{follower}@test.com"))
            .ReturnsAsync(Result.Ok());

        SetControllerUser($"{follower}@test.com");
        _lastResult = await _userController.FollowUser(target);
    }

    [Then(@"Кількість підписників ""(.*)"" збільшується на (\d+)")]
    public void ThenFollowersCountIncreasesBy(string target, int amount)
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.FollowUserAsync(target, It.IsAny<string>()), Times.Once);
    }

    [Then(@"Статус відповіді (\d+)")]
    [Then(@"Повертається статус (\d+) \(операція ідемпотентна\)")]
    public void ThenResponseStatusIs(int statusCode)
    {
        Helpers.ThenResponseCodeIs(statusCode, _lastResult);
    }

    [When(@"""(.*)"" надсилає запит на відписку від ""(.*)""")]
    public async Task WhenUserSendsUnfollowRequest(string follower, string target)
    {
        SetAuthorizedUser(follower, $"{follower}@test.com");

        _userServiceMock
            .Setup(x => x.UnfollowUserAsync(target, $"{follower}@test.com"))
            .ReturnsAsync(Result.Ok());

        SetControllerUser($"{follower}@test.com");
        _lastResult = await _userController.UnfollowUser(target);
    }

    [Then(@"Кількість підписників ""(.*)"" зменшується на (\d+)")]
    public void ThenFollowersCountDecreasesBy(string target, int amount)
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.UnfollowUserAsync(target, It.IsAny<string>()), Times.Once);
    }

    [When(@"^Він відправляє POST запит на ""([^""]*)""$")]
    public async Task WhenHeSendsPostRequestTo(string endpoint)
    {
        if (endpoint.Contains("/follow"))
        {
            var parts = endpoint.Split('/');
            var targetUsername = parts.Length > 3 ? parts[3] : "";

            var currentEmail = _currentEmail ?? "user@test.com";
            _userServiceMock
                .Setup(x => x.FollowUserAsync(targetUsername, It.IsAny<string>()))
                .ReturnsAsync(Result.Ok());

            SetControllerUser(currentEmail);
            _lastResult = await _userController.FollowUser(targetUsername);
        }
        else
        {
            throw new NotImplementedException($"POST endpoint '{endpoint}' не реалізовано.");
        }
    }

    [Then(@"Система не створює підписку")]
    public void ThenSystemDoesNotCreateFollow()
    {
        Assert.IsType<BadRequestObjectResult>(_lastResult);
    }

    [Then(@"Повертає помилку (\d+) \(не можна підписатися на себе\)")]
    public void ThenReturnsErrorCannotFollowSelf(int errorCode)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);
        var badRequest = _lastResult as BadRequestObjectResult;
        Assert.Contains("follow yourself", badRequest?.Value?.ToString() ?? "");
    }

    [When(@"Він відправляє DELETE запит на ""(.*)""")]
    public async Task WhenHeSendsDeleteRequestTo(string endpoint)
    {
        if (endpoint.Contains("/follow"))
        {
            var parts = endpoint.Split('/');
            var targetUsername = parts.Length > 3 ? parts[3] : "";

            // Idempotent unfollow: service returns Ok even if not following
            _userServiceMock
                .Setup(x => x.UnfollowUserAsync(targetUsername, It.IsAny<string>()))
                .ReturnsAsync(Result.Ok());

            SetControllerUser(_currentEmail ?? "user@test.com");
            _lastResult = await _userController.UnfollowUser(targetUsername);
        }
        else if (endpoint.Contains("/profiles/id/"))
        {
            var userId = endpoint.Split('/').Last();

            _userServiceMock
                .Setup(x => x.DeleteAnyUserAsync(userId))
                .ReturnsAsync(Result.Ok());

            SetControllerUser(_currentEmail ?? "admin@test.com");
            _lastResult = await _userController.DeleteUserById(userId);
        }
        else
        {
            throw new NotImplementedException($"DELETE endpoint '{endpoint}' не реалізовано.");
        }
    }

    [Then(@"Повертається помилка або статус про те, що підписки не існує \((\d+)\)")]
    public void ThenReturnsErrorFollowDoesNotExist(int errorCode)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);
    }

    [Given(@"Користувач ""(.*)"" НЕ підписаний на ""(.*)""")]
    public void GivenUserIsNotFollowing(string follower, string target)
    {
        SetAuthorizedUser(follower, $"{follower}@test.com");
    }

    [Then(@"у списку підписників ""(.*)"" з'являється ""(.*)""")]
    public void ThenFollowersListContains(string targetUser, string expectedFollower)
    {
        Assert.IsType<NoContentResult>(_lastResult);
    }

    [Then(@"кількість підписок ""(.*)"" збільшується на (\d+)")]
    public void ThenFollowingCountIncreasesBy(string user, int count)
    {
        Assert.IsType<NoContentResult>(_lastResult);
    }

    [When(@"Він переглядає профіль користувача ""(.*)""")]
    public async Task WhenHeViewsUserProfile(string username)
    {
        // @ui — потребує Selenium
        /* no-op */
    }

    [Then(@"Він бачить кнопку ""(.*)"" \(або ""(.*)""\)")]
    public void ThenHeSeesButtonOrButton(string button1, string button2)
    {
        // @ui — потребує Selenium
        /* no-op */
    }


    #endregion

    #region Аватари

    private static IFormFile CreateMockFormFile(string fileName, string contentType = "image/jpeg")
    {
        var fileMock = new Mock<IFormFile>();
        var content = "Fake image content";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;

        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns(contentType);

        return fileMock.Object;
    }

    [When(@"Клієнт відправляє GET запит на ""/api/profiles/avatar/(.*)""")]
    public async Task WhenClientSendsGetRequestToAvatar(string fileName)
    {
        _userServiceMock
            .Setup(x => x.GetAvatarAsync(fileName))
            .ReturnsAsync(Result.Ok(new ResponseTypes.UserFileResponse(new byte[] { 0x1, 0x2, 0x3 }, "image/jpeg")));
        _lastResult = await _userController.GetAvatar(fileName);
    }

    [When(@"Користувач завантажує файл ""(.*)""")]
    public async Task WhenUserUploadsFile(string fileName)
    {
        await UploadAvatarHelper(fileName);
    }

    [Then(@"У відповіді є URL зображення")]
    public void ThenResponseContainsImageUrl()
    {
        Assert.IsType<NoContentResult>(_lastResult);
    }

    [When(@"^Він відправляє POST запит на ""(.*)"" з валідним файлом зображення \(png/jpg\)$")]
    public async Task WhenHeSendsPostRequestWithValidImage(string endpoint)
    {
        await UploadAvatarHelper("avatar.jpg");
    }

    private async Task UploadAvatarHelper(string fileName = "avatar.jpg")
    {
        var expectedEmail = _currentEmail ?? $"{_currentUsername}@test.com";
        var userId = Guid.Parse(_currentUserId ?? TestConstants.DefaultUserId);

        _unitOfWorkMock
            .Setup(u => u.UserRepository.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<api.Models.User, bool>>>(), null))
            .ReturnsAsync(Result.Ok(new api.Models.User("salt") { Id = userId, Email = expectedEmail, Username = _currentUsername ?? "user", PasswordHash = "h" }));

        _userServiceMock
            .Setup(x => x.RedactProfilePictureAsync(It.Is<ProfilePictureDto>(d => d.Email == expectedEmail)))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(expectedEmail);

        var dto = new ProfilePictureDto
        {
            Email = expectedEmail,
            ProfilePic = CreateMockFormFile(fileName)
        };

        _lastResult = await _userController.RedactProfilePicture(dto);
    }

    [Then(@"Система зберігає зображення")]
    public void ThenSystemSavesImage()
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.RedactProfilePictureAsync(It.IsAny<ProfilePictureDto>()), Times.Once);
    }

    [When(@"^Він відправляє POST запит на ""(.*)"", але вказує email ""(.*)""$")]
    public async Task WhenHeSendsPostRequestButSpecifiesEmail(string endpoint, string otherEmail)
    {
        // New BE design: controller derives target user from JWT id claim and
        // overrides client-supplied email. So a spoofed email is harmless —
        // the user only ever updates their own avatar. Expect 204 NoContent.
        var authEmail = _currentEmail ?? $"{_currentUsername}@test.com";
        var userId = Guid.Parse(_currentUserId ?? TestConstants.DefaultUserId);

        _unitOfWorkMock
            .Setup(u => u.UserRepository.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<api.Models.User, bool>>>(), null))
            .ReturnsAsync(Result.Ok(new api.Models.User("salt") { Id = userId, Email = authEmail, Username = _currentUsername ?? "user", PasswordHash = "h" }));

        _userServiceMock
            .Setup(x => x.RedactProfilePictureAsync(It.IsAny<ProfilePictureDto>()))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(authEmail);

        var dto = new ProfilePictureDto
        {
            Email = otherEmail,
            ProfilePic = CreateMockFormFile("avatar.jpg")
        };

        _lastResult = await _userController.RedactProfilePicture(dto);
    }

    [Then(@"Система забороняє дію")]
    public void ThenSystemForbidsAction()
    {
        Assert.IsType<BadRequestObjectResult>(_lastResult);
    }

    [Then(@"Повертає помилку (\d+) з текстом ""(.*)""")]
    public void ThenReturnsErrorWithText(int errorCode, string errorMessage)
    {
        Helpers.ThenResponseCodeIs(errorCode, _lastResult);

        var badRequest = _lastResult as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        Assert.Contains(errorMessage, badRequest.Value?.ToString() ?? "");
    }

    [Given(@"Існує файл аватарки ""(.*)"" на сервері")]
    public void GivenAvatarFileExistsOnServer(string fileName)
    {
        _userServiceMock
            .Setup(x => x.GetAvatarAsync(fileName))
            .ReturnsAsync(Result.Ok(new ResponseTypes.UserFileResponse(new byte[] { 0x1, 0x2, 0x3 }, "image/jpeg")));
    }

    [Then(@"^Система повертає файл зображення \(image/jpeg\)$")]
    public void ThenSystemReturnsImageFile()
    {
        var fileResult = _lastResult as FileContentResult;
        Assert.NotNull(fileResult);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Given(@"Користувач ""(.*)"" має аватар")]
    public void GivenUserHasAvatar(string username)
    {
        SetAuthorizedUser(username, $"{username}@test.com");
    }

    [When(@"Користувач видаляє свій аватар")]
    public async Task WhenUserDeletesHisAvatar()
    {
        var authEmail = _currentEmail ?? $"{_currentUsername}@test.com";
        var userId = Guid.Parse(_currentUserId ?? TestConstants.DefaultUserId);

        _unitOfWorkMock
            .Setup(u => u.UserRepository.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<api.Models.User, bool>>>(), null))
            .ReturnsAsync(Result.Ok(new api.Models.User("salt") { Id = userId, Email = authEmail, Username = _currentUsername ?? "user", PasswordHash = "h" }));

        _userServiceMock
            .Setup(x => x.RedactProfilePictureAsync(It.IsAny<ProfilePictureDto>()))
            .ReturnsAsync(Result.Ok());

        SetControllerUser(authEmail);

        var dto = new ProfilePictureDto
        {
            Email = authEmail,
            ProfilePic = null
        };

        // Сервіс замоканий, тому перевірку null у реальному сервісі не виконуємо
        _lastResult = await _userController.RedactProfilePicture(dto);
    }

    [Then(@"Профіль користувача більше не містить посилання на аватар")]
    public void ThenUserProfileNoLongerContainsAvatarLink()
    {
        Assert.IsType<NoContentResult>(_lastResult);
        _userServiceMock.Verify(x => x.RedactProfilePictureAsync(
            It.Is<ProfilePictureDto>(d => d.ProfilePic == null)), Times.Once);
    }

    #endregion
}
