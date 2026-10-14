using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RefInfoAPI.Core.Interfaces;
using RefInfoAPI.Data;
using RefInfoAPI.Data.Efcore;
using RefInfoAPI.Host.Auth;
using RefInfoAPI.Host.Controllers;
using ApiUser = RefInfoAPI.Core.Models.User;
using UserGroups = RefInfoAPI.Core.Models.UserGroups;

namespace RefInfoAPI.Tests;

public class AuthControllerTests
{
    private readonly ApiUser _user = new(1, "test", "organisation-password", UserGroups.Drugstore);

    [Fact]
    public async Task Login_UsesExistingOrganisationAccount_WithoutImportOrSeparateStorage()
    {
        var users = Substitute.For<IUserRepository>();
        users.Get("test", Arg.Any<CancellationToken>()).Returns(_user);
        var jwt = TestJwt.Create();
        var controller = new AuthController(users, jwt);

        var response = Assert.IsType<OkObjectResult>(await controller.Login(new LoginRequest("test", _user.Password)));
        var token = Assert.IsType<TokenResponse>(response.Value);
        var principal = jwt.GetClaimsPrincipal(token.AccessToken);
        Assert.Equal("1", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Drugstore", principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(jwt.GetCredentialVersion(1, _user.Password), principal.FindFirstValue("api_credential_version"));
        Assert.DoesNotContain(principal.Claims, claim => claim.Value.Contains(_user.Password));
    }

    [Theory]
    [InlineData("wrong-password", false)]
    [InlineData("organisation-password", true)]
    public async Task Login_RejectsWrongPasswordOrMissingAccount(string password, bool missing)
    {
        var users = Substitute.For<IUserRepository>();
        users.Get("test", Arg.Any<CancellationToken>()).Returns(missing ? null : _user);
        var controller = new AuthController(users, TestJwt.Create());
        Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginRequest("test", password)));
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData(" ", "password")]
    [InlineData("test", "")]
    [InlineData("test", " ")]
    public async Task Login_EmptyCredentialsAreRejected(string username, string password)
    {
        var users = Substitute.For<IUserRepository>();
        var controller = new AuthController(users, TestJwt.Create());
        Assert.IsType<BadRequestObjectResult>(await controller.Login(new LoginRequest(username, password)));
        await users.DidNotReceive().Get(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_TrimsUsername_ButPreservesPasswordExactly()
    {
        var users = Substitute.For<IUserRepository>();
        users.Get("test", Arg.Any<CancellationToken>()).Returns(_user with { Password = " password with spaces " });
        var controller = new AuthController(users, TestJwt.Create());
        Assert.IsType<OkObjectResult>(await controller.Login(new LoginRequest(" test ", " password with spaces ")));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginRequest("test", "password with spaces")));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("password-changed")]
    [InlineData("deleted")]
    [InlineData("id-changed")]
    [InlineData("missing-version")]
    public async Task Refresh_RejectsInvalidExpiredOrChangedOrganisationCredentials(string scenario)
    {
        var jwt = TestJwt.Create();
        var signer = TestJwt.Create(scenario == "issuer" ? "wrong" : "test-issuer",
            scenario == "audience" ? "wrong" : "test-audience");
        var claims = Claims(signer);
        if (scenario == "missing-version")
            claims.RemoveAll(claim => claim.Type == "api_credential_version");
        var raw = signer.WriteToken(signer.CreateToken(claims,
            scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(10)));
        var users = Substitute.For<IUserRepository>();
        var current = scenario switch
        {
            "deleted" => null,
            "password-changed" => _user with { Password = "new-organisation-password" },
            "id-changed" => _user with { Id = 2 },
            _ => _user
        };
        users.Get("test", Arg.Any<CancellationToken>()).Returns(current);
        var controller = new AuthController(users, jwt);
        Assert.IsType<UnauthorizedObjectResult>(await controller.Refresh("Bearer " + raw));
    }

    [Fact]
    public async Task Refresh_RebuildsClaimsUsingTheCurrentOrganisationRole()
    {
        var jwt = TestJwt.Create();
        var raw = jwt.WriteToken(jwt.CreateToken(Claims(jwt), DateTime.UtcNow.AddMinutes(10)));
        var users = Substitute.For<IUserRepository>();
        users.Get("test", Arg.Any<CancellationToken>()).Returns(_user);
        var controller = new AuthController(users, jwt);

        var response = Assert.IsType<OkObjectResult>(await controller.Refresh("bearer " + raw));
        var token = Assert.IsType<TokenResponse>(response.Value);
        Assert.Equal("Drugstore", jwt.GetClaimsPrincipal(token.AccessToken).FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task Repository_LoginImmediatelyUsesAPasswordChangeInTheOrganisationDatabase()
    {
        var options = new DbContextOptionsBuilder<RefInfoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new RefInfoDbContext(options);
        var entity = new RefInfoAPI.Data.Entities.User
        {
            Id = 1, Username = "test", Password = _user.Password, Group = (int)UserGroups.Drugstore
        };
        db.Users.Add(entity);
        await db.SaveChangesAsync();
        var repository = new EfUserRepository(db);
        var jwt = TestJwt.Create();
        var controller = new AuthController(repository, jwt);
        var loggedIn = Assert.IsType<OkObjectResult>(await controller.Login(new LoginRequest("test", _user.Password)));
        var oldToken = Assert.IsType<TokenResponse>(loggedIn.Value);

        entity.Password = "new-organisation-password";
        await db.SaveChangesAsync();

        Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginRequest("test", _user.Password)));
        Assert.IsType<OkObjectResult>(await controller.Login(new LoginRequest("test", entity.Password)));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Refresh("Bearer " + oldToken.AccessToken));
    }

    private List<Claim> Claims(JwtService jwt) =>
    [
        new(ClaimTypes.NameIdentifier, "1"),
        new(ClaimTypes.Name, "test"),
        new(ClaimTypes.Role, "Admin"),
        new("api_credential_version", jwt.GetCredentialVersion(_user.Id, _user.Password))
    ];
}
