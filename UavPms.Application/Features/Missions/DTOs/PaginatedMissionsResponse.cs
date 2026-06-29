using UavPms.Application.Features.Users.DTOs;

namespace UavPms.Application.Features.Missions.DTOs;

public record PaginatedMissionsResponse(
    List<MissionDto> Items,
    PaginationMetaData Pagination);