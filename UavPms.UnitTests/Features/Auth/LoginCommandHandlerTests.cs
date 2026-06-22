using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.Application.Features.Auth.Commands.Login;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.UnitTests.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtProvider> _jwtProviderMock;
    private readonly Mock<IGenericRepository<RefreshToken>> _refreshTokenRepositoryMock;
    private readonly Mock<IOtpService> _otpServiceMock;
    private readonly Mock<IGenericRepository<TrustedDevice>> _trustedDeviceRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtProviderMock = new Mock<IJwtProvider>();
        _refreshTokenRepositoryMock = new Mock<IGenericRepository<RefreshToken>>();
        _otpServiceMock = new Mock<IOtpService>();
        _trustedDeviceRepositoryMock = new Mock<IGenericRepository<TrustedDevice>>();
        _configurationMock = new Mock<IConfiguration>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _configurationMock.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("60");
        
        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _jwtProviderMock.Object,
            _refreshTokenRepositoryMock.Object,
            _otpServiceMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _configurationMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task Hanlde_ShouldThrowUnauthorizedException_WhenUserDoesNotExist()
    {
        var command = new LoginCommand("nonexistuser@gmail.com", "password123", null, "UserAgent");
        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.GetByUsernameWithRolesAsync(command.Email)).ReturnsAsync((User?)null);
        
        // act 
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        //assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedException_WhenUserIsInactive()
    {
        // arrange
        var command = new LoginCommand("inactiveuser@gmail.com", "password123", null, "UserAgent");
        var user = new User
        {
            Email = command.Email,
            Status = "Inactive"
        };
        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync(user);
        
        //act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedException_WhenPasswordIsIncorrect()
    {
        
    }
}