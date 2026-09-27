using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class PreMissionAssessment : BaseEntity
{
    public Guid ManagerId { get; set; }
    public Guid RegionId { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public Geometry? ProposedBoundary { get; set; }
    public ReadinessCheckStatus SiteFeasibilityStatus { get; set; } = ReadinessCheckStatus.Pending;

    [JsonIgnore]
    public PreMissionAssessmentStatus Status { get; set; } = PreMissionAssessmentStatus.Draft;

    [NotMapped]
    [JsonPropertyName("status")]
    public string StatusText => Status switch
    {
        PreMissionAssessmentStatus.Draft => "DRAFT",
        PreMissionAssessmentStatus.Evaluating => "EVALUATING",
        PreMissionAssessmentStatus.Ready => "READY",
        PreMissionAssessmentStatus.NotReady => "NOT_READY",
        PreMissionAssessmentStatus.Expired => "EXPIRED",
        PreMissionAssessmentStatus.Completed => "COMPLETED",
        PreMissionAssessmentStatus.Cancelled => "CANCELLED",
        _ => Status.ToString().ToUpperInvariant()
    };

    public TechnicalHealth OverallTechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public DateTime? ValidUntil { get; set; }
    public Guid? ConsumedByMissionId { get; set; }
    public uint Version { get; set; } = 1;
    public string Findings { get; set; } = "{}";
    public string? EvaluationPolicyVersion { get; set; }
    public string? IdempotencyKey { get; set; }
    public virtual Region? Region { get; set; }
    public virtual User? Manager { get; set; }
    public ICollection<PreMissionAssessmentAsset> Assets { get; set; } = new List<PreMissionAssessmentAsset>();
    public ICollection<PreMissionAssessmentPersonnel> PersonnelCandidates { get; set; } = new List<PreMissionAssessmentPersonnel>();
    public ICollection<PreMissionAssessmentDrone> DroneCandidates { get; set; } = new List<PreMissionAssessmentDrone>();

    [NotMapped]
    [JsonPropertyName("uavCandidates")]
    public IEnumerable<object> UavCandidates => DroneCandidates.Select(d => new
    {
        id = d.DroneId,
        droneId = d.DroneId,
        code = d.Drone?.UavCode ?? d.DroneId.ToString()[..8],
        uavCode = d.Drone?.UavCode ?? d.DroneId.ToString()[..8],
        name = !string.IsNullOrEmpty(d.Drone?.Model) ? $"{d.Drone.Model} ({d.Drone.UavCode})" : (d.Drone?.UavCode ?? "UAV"),
        model = d.Drone?.Model ?? "UAV",
        status = d.OperationalAvailabilityStatus.ToString(),
        operationalStatus = d.OperationalAvailabilityStatus.ToString(),
        technicalHealth = d.TechnicalHealth.ToString(),
        isEligible = d.IsEligible,
        isAvailable = d.OperationalAvailabilityStatus == ResourceAvailabilityStatus.Available,
        batteryLevel = d.Drone?.BatteryLevel ?? 100,
        reason = d.ReasonCode,
        technicalInspectionId = d.TechnicalInspectionId
    });

    [NotMapped]
    [JsonPropertyName("site")]
    public object SiteCheck => new
    {
        status = SiteFeasibilityStatus == ReadinessCheckStatus.Passed ? "PASS" : "FAIL",
        reason = SiteFeasibilityStatus == ReadinessCheckStatus.Passed ? null : "Mặt bằng hành lang chưa đạt điều kiện an toàn.",
        evaluatedAt = DateTime.UtcNow
    };

    [NotMapped]
    [JsonPropertyName("uav")]
    public object UavCheck => new
    {
        status = DroneCandidates.Any(d => d.IsEligible) ? "PASS" : "FAIL",
        reason = DroneCandidates.Any(d => d.IsEligible) ? null : "Chưa có UAV nào đạt hạn kiểm định hoặc khả dụng trong khung giờ này.",
        evaluatedAt = DateTime.UtcNow
    };

    [NotMapped]
    [JsonPropertyName("technical")]
    public object TechnicalCheck => new
    {
        status = (OverallTechnicalHealth is TechnicalHealth.Healthy or TechnicalHealth.Warning) && DroneCandidates.Any(d => d.IsEligible) ? "PASS" : "FAIL",
        reason = (OverallTechnicalHealth is TechnicalHealth.Healthy or TechnicalHealth.Warning) && DroneCandidates.Any(d => d.IsEligible) ? null : "Cần thực hiện kiểm định kỹ thuật tự động (BIST/Telemetry) trước khi bay.",
        evaluatedAt = DateTime.UtcNow
    };

    [NotMapped]
    [JsonPropertyName("personnel")]
    public object PersonnelCheck => new
    {
        status = (PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Inspector") &&
                  PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Analyst") &&
                  PersonnelCandidates.Any(p => p.IsEligible && (p.Role == "Technician" || p.Role == "MaintenanceTechnician"))) ? "PASS" : "FAIL",
        reason = (PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Inspector") &&
                  PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Analyst") &&
                  PersonnelCandidates.Any(p => p.IsEligible && (p.Role == "Technician" || p.Role == "MaintenanceTechnician"))) ? null : "Chưa đạt định mức nhân sự đang rảnh.",
        evaluatedAt = DateTime.UtcNow
    };

    [NotMapped]
    public int RequiredInspectors { get; set; } = 1;
    [NotMapped]
    public int RequiredAnalysts { get; set; } = 1;
    [NotMapped]
    public int RequiredTechnicians { get; set; } = 1;
}

public sealed class PreMissionAssessmentAsset : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid AssetId { get; set; }
    public int Sequence { get; set; }
    [JsonIgnore]
    public PreMissionAssessment? Assessment { get; set; }
    public Asset? Asset { get; set; }
}

public sealed class PreMissionAssessmentPersonnel : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public bool IsEligible { get; set; }
    public ResourceEligibilityStatus EligibilityStatus { get; set; } = ResourceEligibilityStatus.Unknown;
    public ResourceAvailabilityStatus AvailabilityStatus { get; set; } = ResourceAvailabilityStatus.Unknown;
    public string? ReasonCode { get; set; }
    public DateTime SnapshotAt { get; set; }
    public string Findings { get; set; } = "{}";
    [JsonIgnore]
    public PreMissionAssessment? Assessment { get; set; }
    public User? User { get; set; }

    [NotMapped]
    public string Role
    {
        get
        {
            var r = User?.UserRoles?.FirstOrDefault(ur => ur.Role != null)?.Role?.RoleName;
            if (!string.IsNullOrEmpty(r)) return r;
            try
            {
                if (!string.IsNullOrEmpty(Findings) && Findings.Contains("\"role\""))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(Findings);
                    if (doc.RootElement.TryGetProperty("role", out var el))
                        return el.GetString() ?? "Inspector";
                }
            }
            catch { }
            return "Inspector";
        }
    }

    [NotMapped]
    public string Name => User?.FullName ?? string.Empty;

    [NotMapped]
    public string FullName => User?.FullName ?? string.Empty;

    [NotMapped]
    public bool IsActive => User != null ? (User.IsEmailVerified && (User.Status == "Active" || User.Status == "Enabled")) : true;

    [NotMapped]
    public bool IsWithinScope => ReasonCode != "OUTSIDE_MANAGEMENT_SCOPE";

    [NotMapped]
    public bool IsAvailable => AvailabilityStatus == ResourceAvailabilityStatus.Available;

    [NotMapped]
    public string OverallEligibility => IsEligible ? "ELIGIBLE" : "INELIGIBLE";
}

public sealed class PreMissionAssessmentDrone : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid DroneId { get; set; }
    public bool IsEligible { get; set; }
    public ResourceAvailabilityStatus OperationalAvailabilityStatus { get; set; } = ResourceAvailabilityStatus.Unknown;
    public ResourceEligibilityStatus TechnicalEligibilityStatus { get; set; } = ResourceEligibilityStatus.Unknown;
    public string? ReasonCode { get; set; }
    public DateTime SnapshotAt { get; set; }
    public TechnicalHealth TechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public Guid? TechnicalInspectionId { get; set; }
    [JsonIgnore]
    public PreMissionAssessment? Assessment { get; set; }
    public Uav? Drone { get; set; }
    public DroneTechnicalInspection? TechnicalInspection { get; set; }

    [NotMapped]
    public string Code => Drone?.UavCode ?? DroneId.ToString()[..8];

    [NotMapped]
    public string UavCode => Drone?.UavCode ?? DroneId.ToString()[..8];

    [NotMapped]
    public string Name => Drone != null ? $"{Drone.Model} ({Drone.UavCode})" : (Drone?.UavCode ?? "UAV");

    [NotMapped]
    public string Model => Drone?.Model ?? "UAV";

    [NotMapped]
    public string OperationalStatus => OperationalAvailabilityStatus.ToString();

    [NotMapped]
    public double BatteryLevel => Drone?.BatteryLevel ?? 100;
}

public sealed class DroneTechnicalInspection : BaseEntity
{
    public Guid DroneId { get; set; }
    public Guid? TechnicianUserId { get; set; }
    public DroneTechnicalInspectionStatus Status { get; set; } = DroneTechnicalInspectionStatus.Pending;
    public TechnicalHealth Health { get; set; } = TechnicalHealth.Unknown;
    public string SourceType { get; set; } = "manual";
    public string SourceVersion { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public string RawSnapshot { get; set; } = "{}";
    public DateTime? CompletedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? PolicyVersion { get; set; }
    public string? Notes { get; set; }
    public uint Version { get; set; } = 1;
    public Uav? Drone { get; set; }
    public User? Technician { get; set; }
    public ICollection<DroneTechnicalMetric> Metrics { get; set; } = new List<DroneTechnicalMetric>();
}

public sealed class DroneTechnicalMetric : BaseEntity
{
    public Guid InspectionId { get; set; }
    public string MetricCode { get; set; } = string.Empty;
    public string Subsystem { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public bool Passed { get; set; }
    public bool Critical { get; set; }
    public string? ValueText { get; set; }
    public bool? BoolValue { get; set; }
    public string? Unit { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Metadata { get; set; }
    public DroneTechnicalInspection? Inspection { get; set; }
}
