using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandValidator : AbstractValidator<UploadInspectionImageCommand>
{
    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg", "image/webp", "image/png", "image/tiff",
        "video/mp4", "video/quicktime", "video/webm"
    };
    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".webp", ".png", ".tif", ".tiff",
        ".mp4", ".mov", ".webm"
    };

    private readonly long _maxImageSizeBytes;
    private readonly long _maxVideoSizeBytes;

    public UploadInspectionImageCommandValidator(IConfiguration? configuration = null)
    {
        _maxImageSizeBytes = long.TryParse(configuration?["InspectionMedia:MaxFileSizeBytes"], out var configuredImageLimit) && configuredImageLimit > 0
            ? configuredImageLimit
            : 50L * 1024 * 1024;
        _maxVideoSizeBytes = long.TryParse(configuration?["InspectionMedia:MaxVideoSizeBytes"], out var configuredVideoLimit) && configuredVideoLimit > 0
            ? configuredVideoLimit
            : 500L * 1024 * 1024;
        RuleFor(x => x.MissionId).NotEmpty().WithMessage("Mission ID is required.");
        
        RuleFor(x => x.TowerId).Must(id => id != Guid.Empty).When(x => x.TowerId.HasValue).WithMessage("Tower ID is invalid.");
        
        RuleFor(x => x.FileStream).NotEmpty().WithMessage("File stream is required.");

        RuleFor(x => x.FileStream).NotNull().WithMessage("File stream cannot be null.")
             .Must(stream => stream != null && stream.Length > 0).WithMessage("Media file is required.");

        RuleFor(x => x)
             .Must(x => x.FileStream != null && x.FileStream.Length <= MaxSizeFor(x.ContentType, x.FileName))
             .When(x => x.FileStream != null)
             .WithMessage(x =>
             {
                 var limitMb = MaxSizeFor(x.ContentType, x.FileName) / 1024 / 1024;
                 return $"File size exceeds the {limitMb}MB limit.";
             });
         
        RuleFor(x => x.ContentType).NotEmpty().WithMessage("Content type is required.")
             .Must(ct => !string.IsNullOrEmpty(ct) && AllowedContentTypes.Contains(ct.ToLower()))
             .WithMessage("Invalid file type. Allowed: JPEG, PNG, WebP, TIFF, MP4, MOV, WebM.");

        RuleFor(x => x.FileName).NotEmpty()
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(Path.GetFileName(name)).ToLowerInvariant()))
            .WithMessage("Invalid file extension. Allowed: JPEG, PNG, WebP, TIFF, MP4, MOV, WebM.");

        RuleFor(x => x)
            .Must(x => ExtensionMatchesMimeType(x.FileName, x.ContentType))
            .WithMessage("File extension does not match the declared MIME type.");

        RuleFor(x => x)
            .Must(x => InspectionMediaFileValidator.IsValid(x.FileStream, x.ContentType))
            .WithMessage("The file signature is invalid or the media cannot be decoded.");

        RuleFor(x => x.CapturedAt)
            .Must(value => value != default && value.ToUniversalTime() <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Capture timestamp is invalid.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Latitude and longitude must be supplied together.");
    }

    private long MaxSizeFor(string? contentType, string? fileName)
    {
        var isVideo = (!string.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            || Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).ToLowerInvariant() is ".mp4" or ".mov" or ".webm";
        return isVideo ? _maxVideoSizeBytes : _maxImageSizeBytes;
    }

    private static bool ExtensionMatchesMimeType(string fileName, string contentType)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            "image/webp" => extension == ".webp",
            "image/tiff" => extension is ".tif" or ".tiff",
            "video/mp4" => extension == ".mp4",
            "video/quicktime" => extension == ".mov",
            "video/webm" => extension == ".webm",
            _ => false
        };
    }
}
