using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.Core.Entities;
using UavPms.Infrastructure.Services;

namespace UavPms.UnitTests.Services;

public class JwtProviderTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly JwtProvider _jwtProvider;
    private const string SecretKey = "super_secret_key_123_456_789_0123_345";
    private const string Issuer = "UavPms_Issuer";
    private const string Audience = "UavPms_Audience";
    
    public JwtProviderTests()
    {
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["Jwt:SecretKey"]).Returns(SecretKey);
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns(Issuer);
        _configMock.Setup(c => c["Jwt:Audience"]).Returns(Audience);
        _configMock.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("60");
        
        _jwtProvider = new JwtProvider(_configMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken_WithCorrectClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testusername",
            Email = "testuseremail@gmail.com",
            FullName = "testuserfullname",
        };

        var roles = new List<string> { "Admin", "Operator" };
        
        var tokenString =  _jwtProvider.GenerateAccessToken(user, roles);
        
        tokenString.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);
        
        jwtToken.Should().NotBeNull();
        jwtToken.Issuer.Should().Be(Issuer);
        jwtToken.Audiences.Should().Contain(Audience);
        
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == "fullname" && c.Value == user.FullName);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value);
        roleClaims.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyString()
    {
        var tokenString1 = _jwtProvider.GenerateRefreshToken();
        var tokenString2 = _jwtProvider.GenerateRefreshToken();
        tokenString1.Should().NotBe(tokenString2);
        tokenString1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnValidToken_WithStepUpClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
        };
        var purpose = "ChangePassword";

        var tokenString = _jwtProvider.GenerateStepUpToken(user, purpose);
        
        tokenString.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);
        
        jwtToken.Should().NotBeNull();
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == "step_up_purpose" && c.Value == purpose);
        jwtToken.Claims.Should().Contain(c => c.Type == "step_up_verified_at");
    }
}