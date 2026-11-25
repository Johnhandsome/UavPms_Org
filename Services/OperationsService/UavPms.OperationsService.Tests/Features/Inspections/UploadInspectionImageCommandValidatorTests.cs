using System;
using System.IO;
using FluentAssertions;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class UploadInspectionImageCommandValidatorTests
{
    private readonly UploadInspectionImageCommandValidator _validator;

    public UploadInspectionImageCommandValidatorTests()
    {
        _validator = new UploadInspectionImageCommandValidator();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenMissionIdIsEmpty()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.Empty,
            TowerId = Guid.NewGuid(),
            FileName = "test.jpg",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MissionId");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenAssetIdIsEmpty()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.Empty,
            FileName = "test.jpg",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TowerId");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenFileStreamIsNull()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "test.jpg",
            FileStream = null!,
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileStream");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenContentTypeIsInvalid()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "document.pdf",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "application/pdf"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentType");
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenRequestIsValid()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "test.jpg",
            FileStream = ValidJpeg(),
            ContentType = "image/jpeg",
            CapturedAt = DateTime.UtcNow
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRejectSpoofedMediaSignature()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "spoofed.jpg",
            FileStream = new MemoryStream("not-an-image"u8.ToArray()),
            ContentType = "image/jpeg",
            CapturedAt = DateTime.UtcNow
        };

        _validator.Validate(command).Errors.Should()
            .Contain(error => error.ErrorMessage.Contains("signature"));
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void Validate_ShouldRejectInvalidGps(double latitude, double longitude)
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(), TowerId = Guid.NewGuid(), FileName = "test.jpg",
            FileStream = ValidJpeg(), ContentType = "image/jpeg", CapturedAt = DateTime.UtcNow,
            Latitude = latitude, Longitude = longitude
        };

        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    private static MemoryStream ValidJpeg() =>
        new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0xFF, 0xD9 });

    [Fact]
    public void Validate_ShouldAllowLargeMp4VideoUpTo500Mb_AndRejectOver500Mb()
    {
        var mp4Header = new byte[] { 0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' };

        var video150Mb = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "corridor_flight.mp4",
            FileStream = new VirtualSizedStream(mp4Header, 150L * 1024 * 1024),
            ContentType = "video/mp4",
            CapturedAt = DateTime.UtcNow
        };
        _validator.Validate(video150Mb).IsValid.Should().BeTrue();

        var video550Mb = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "corridor_too_large.mp4",
            FileStream = new VirtualSizedStream(mp4Header, 550L * 1024 * 1024),
            ContentType = "video/mp4",
            CapturedAt = DateTime.UtcNow
        };
        var tooLarge = _validator.Validate(video550Mb);
        tooLarge.IsValid.Should().BeFalse();
        tooLarge.Errors.Should().Contain(e => e.ErrorMessage.Contains("500MB"));
    }

    [Fact]
    public void Validate_ShouldSupportMovAndWebmVideoContainers()
    {
        var movHeader = new byte[] { 0x00, 0x00, 0x00, 0x14, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'q', (byte)'t', (byte)' ', (byte)' ' };
        var webmHeader = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81 };

        var movCmd = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "flight.mov",
            FileStream = new VirtualSizedStream(movHeader, 80L * 1024 * 1024),
            ContentType = "video/quicktime",
            CapturedAt = DateTime.UtcNow
        };
        _validator.Validate(movCmd).IsValid.Should().BeTrue();

        var webmCmd = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            TowerId = Guid.NewGuid(),
            FileName = "flight.webm",
            FileStream = new VirtualSizedStream(webmHeader, 80L * 1024 * 1024),
            ContentType = "video/webm",
            CapturedAt = DateTime.UtcNow
        };
        _validator.Validate(webmCmd).IsValid.Should().BeTrue();
    }

    private sealed class VirtualSizedStream : Stream
    {
        private readonly byte[] _header;
        private readonly long _length;
        private long _position;

        public VirtualSizedStream(byte[] header, long length)
        {
            _header = header;
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length) return 0;
            var toRead = (int)Math.Min(count, _length - _position);
            for (var i = 0; i < toRead; i++)
            {
                var idx = _position + i;
                buffer[offset + i] = idx < _header.Length ? _header[idx] : (byte)0;
            }
            _position += toRead;
            return toRead;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => _position
            };
            return _position;
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
