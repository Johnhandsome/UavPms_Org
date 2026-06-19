using NetTopologySuite.Geometries;
using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class Tower : BaseEntity
{
    public Guid LineAssetId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public Geometry? Geom { get; set; }

    public virtual TransmissionLine? TransmissionLine { get; set; }
    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
