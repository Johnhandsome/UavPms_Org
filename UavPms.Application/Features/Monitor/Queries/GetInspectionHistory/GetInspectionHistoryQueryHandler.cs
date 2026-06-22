using MediatR;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Monitor.Queries.GetInspectionHistory;

public class GetInspectionHistoryQueryHandler : IRequestHandler<GetInspectionHistoryQuery, InspectionHistoryResponse>
{
    private readonly IMonitorRepository _monitorRepository;

    public GetInspectionHistoryQueryHandler(IMonitorRepository monitorRepository)
    {
        _monitorRepository = monitorRepository;
    }

    public async Task<InspectionHistoryResponse> Handle(GetInspectionHistoryQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _monitorRepository.GetInspectionHistoryAsync(
            request.MissionId, 
            request.IsDefect, 
            request.FromDate, 
            request.ToDate, 
            request.Page, 
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(x => new InspectionHistoryDto(
            x.InspectionId,
            x.MissionId,
            x.MissionTitle,
            x.ImageUrl,
            x.IsDefect,
            x.DefectType,
            x.DetectedAt))
            .ToList();

        return new InspectionHistoryResponse(dtos, totalCount);
    }
}
