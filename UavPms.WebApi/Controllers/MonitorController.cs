using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.Application.Features.Monitor.Queries.GetActiveAlerts;
using UavPms.Application.Features.Monitor.Queries.GetDashboardSummary;
using UavPms.Application.Features.Monitor.Queries.GetDefectStatistics;
using UavPms.Application.Features.Monitor.Queries.GetInspectionHistory;
using UavPms.Application.Features.Monitor.Queries.GetMissionStatusOverview;
using UavPms.Application.Features.Monitor.Queries.GetRecentDefects;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/monitor")]
[ApiVersion("1.0")]
public class MonitorController : ControllerBase
{
    private readonly ISender _mediator;

    public MonitorController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _mediator.Send(new GetDashboardSummaryQuery());
        return Ok(result);
    }

    [HttpGet("recent-defects")]
    public async Task<IActionResult> GetRecentDefects([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new GetRecentDefectsQuery(page, pageSize));
        return Ok(result);
    }

    [HttpGet("defects-statistics")]
    public async Task<IActionResult> GetDefectStatistics()
    {
        var result = await _mediator.Send(new GetDefectStatisticsQuery());
        return Ok(result);
    }

    [HttpGet("mission-status")]
    public async Task<IActionResult> GetMissionStatus()
    {
        var result = await _mediator.Send(new GetMissionStatusQuery());
        return Ok(result);
    }

    [HttpGet("inspections")]
    public async Task<IActionResult> GetInspections(
        [FromQuery] Guid? missionId,
        [FromQuery] bool? isDefect,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new GetInspectionHistoryQuery(missionId, isDefect, fromDate, toDate, page, pageSize));
        return Ok(result);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetActiveAlerts()
    {
        var result = await _mediator.Send(new GetActiveAlertsQuery());
        return Ok(result);
    }
}
