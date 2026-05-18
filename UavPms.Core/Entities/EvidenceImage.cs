using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class EvidenceImage : BaseEntity
{
    // Khóa ngoại trỏ về Alert
    public Guid DetectionAlertId { get; set; }
    
    public string ImagePath { get; set; } = string.Empty;
    public long ImageSizeBytes { get; set; }
    
    // Navigation Property: Trỏ ngược về Alert mẹ (Quan hệ 1-1)
    public virtual DetectionAlert? DetectionAlert { get; set; }
}