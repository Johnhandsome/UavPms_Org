using MediatR;
using UavPms.Application.Features.Missions.DTOs;

namespace UavPms.Application.Features.Missions.Queries.GetMy;

public record GetMymissionsQuery : IRequest<List<MissionDto>>;