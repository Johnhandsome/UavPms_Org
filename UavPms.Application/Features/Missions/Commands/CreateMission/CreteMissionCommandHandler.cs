using MediatR;
using UavPms.Application.Features.Missions.DTOs;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.Missions.Commands.CreateMission;

public class CreteMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUserServices;
    private readonly IEventPublisher _eventPublisher;
    
    public Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}