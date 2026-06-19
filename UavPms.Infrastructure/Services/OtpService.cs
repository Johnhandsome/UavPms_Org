using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using UavPms.Core.Contracts;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly IEventPublisher _eventPublisher;

    public OtpService(IMemoryCache cache, IEmailService emailService, IEventPublisher eventPublisher)
    {
        _cache = cache;
        _emailService = emailService;
        _eventPublisher = eventPublisher;
    }

    private static string GetCacheKey(string email, OtpPurpose purpose) => $"otp:{email}:{purpose.ToString().ToLower()}";

    public async Task<(bool Success, string Message)> GenerateAndSendOtpAsync(string email, OtpPurpose purpose, bool isResend = false)
    {
        var key = GetCacheKey(email, purpose);
        
        if (isResend && _cache.TryGetValue<OtpCacheItem>(key, out var existingOtp) && existingOtp != null)
        {
            var elapsed = DateTime.UtcNow - existingOtp.CreatedAt;
            if (elapsed < TimeSpan.FromSeconds(30))
            {
                var remaining = 30 - (int)elapsed.TotalSeconds;
                return (false, $"Please wait {remaining} seconds before requesting a new OTP.");
            }
        }

        var code = new Random().Next(100000, 999999).ToString();
        var expiryTime = DateTime.UtcNow.AddMinutes(3);

        var otpItem = new OtpCacheItem
        {
            Code = code,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _emailService.SendOtpEmailAsync(email, code, expiryTime);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to send OTP email: {ex.Message}");
        }

        _cache.Set(key, otpItem, TimeSpan.FromMinutes(3));

        await _eventPublisher.PublishAsync(new OtpGenerated
        {
            Email = email,
            OtpCode = code,
            ExpiryTime = expiryTime
        });

        return (true, "OTP generated and sent successfully.");
    }

    public async Task<(bool IsValid, string Message)> VerifyOtpAsync(string email, string code, OtpPurpose purpose)
    {
        var key = GetCacheKey(email, purpose);

        if (!_cache.TryGetValue<OtpCacheItem>(key, out var otpItem) || otpItem == null)
        {
            return (false, "OTP has expired or does not exist.");
        }

        if (otpItem.Attempts >= 3)
        {
            return (false, "Maximum verification attempts exceeded.");
        }

        if (otpItem.Code != code)
        {
            otpItem.Attempts++;
            var remainingTime = TimeSpan.FromMinutes(3) - (DateTime.UtcNow - otpItem.CreatedAt);
            if (remainingTime > TimeSpan.Zero)
            {
                _cache.Set(key, otpItem, remainingTime);
            }
            else
            {
                _cache.Remove(key);
            }
            return (false, "Invalid OTP code.");
        }

        _cache.Remove(key);
        await Task.CompletedTask;
        return (true, "OTP verified successfully.");
    }
}
