using MediatR;
using UavPms.Application.Features.Missions.DTOs;

namespace UavPms.Application.Features.Missions.Queries.GetDetails;

public record GetMissionDetailsQuery(Guid Id) : IRequest<MissionDto>;