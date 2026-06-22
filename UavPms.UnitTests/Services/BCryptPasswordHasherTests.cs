using UavPms.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace UavPms.UnitTests.Services;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher;

    public  BCryptPasswordHasherTests()
    {
        _hasher = new BCryptPasswordHasher();
    }

    [Fact]
    public void Hash_ShouldReturnHashedPassword_WhenGivenPlanPassword()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenPasswordMatchesHash()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        
        var result = _hasher.Verify(hash, password);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenPasswordDoesNotMatchHash()
    {
        var password = "SuperSecretPassword123!@#";
        var wrongPassword = "WrongPassword123!@#";
        var hash = _hasher.Hash(password);
        
        var result = _hasher.Verify(hash, wrongPassword);
        
        result.Should().BeFalse();
    }
}