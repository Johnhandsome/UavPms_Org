using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Models.Monitor;
using UavPms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static UavPms.Core.Models.Monitor.DefectStatisticsModel;

namespace UavPms.Infrastructure.Repositories
{
    public class MonitorRepository : IMonitorRepository
    {
        private readonly ApplicationDbContext _context;

        public MonitorRepository(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<List<ActiveAlertModel>> GetActiveAlertsAsync(CancellationToken cancellationToken)
        {
            return await _context.Notifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.SentAt)
                .Select(n => new ActiveAlertModel
                {
                    NotificationId = n.Id,
                    Title = n.Title,
                    Message = n.Body,
                    CreatedAt = n.SentAt,
                    IsRead = n.IsRead
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<DefectStatisticsModel> GetDefectStatisticsAsync(CancellationToken cancellationToken)
        {
            var byType = await _context.DetectedAnomalies
                .Where(a => a.ValidationStatus == "Confirmed")
                .GroupBy(a => a.Category.CategoryName)
                .Select(g => new DefectTypeStatModel
                {
                    DefectType = g.Key,
                    Count = g.Count()
                }).ToListAsync(cancellationToken);

            var totalDefects = byType.Sum(x => x.Count);

            return new DefectStatisticsModel
            {
                TotalDefects = totalDefects,
                ByType = byType
            };
        }

        public async Task<(List<InspectionHistoryModel> Items, int TotalCount)> GetInspectionHistoryAsync(Guid? missionId, bool? IsDefect, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = _context.InspectionMedia.AsQueryable();

            if (missionId.HasValue)
            {
                query = query.Where(m => m.MissionId == missionId.Value);
            }

            if(IsDefect.HasValue){
                if(IsDefect.Value)
                {
                    query = query.Where(m => 
                    m.DetectedAnomalies.Any(a => 
                    a.ValidationStatus == "Confirmed"));
                }
                else
                {
                    query = query.Where(m =>
                    !m.DetectedAnomalies.Any(a =>
                        a.ValidationStatus == "Confirmed"));
                }
            }

            if (fromDate.HasValue) 
            { 
                query = query.Where(m => m.CapturedAt >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                query = query.Where(m => m.CapturedAt <= toDate.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(m => m.CapturedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new InspectionHistoryModel
                {
                    InspectionId = m.Id,
                    MissionId = m.MissionId,
                    MissionTitle = !string.IsNullOrEmpty(m.Mission.Description)
                    ? m.Mission.Description : m.Mission.MissionCode,
                    ImageUrl = m.FileUrl,
                    IsDefect = m.DetectedAnomalies.Any(a => a.ValidationStatus == "Confirmed"),
                    DefectType = m.DetectedAnomalies.Where(a => a.ValidationStatus == "Confirmed").Select(a => a.Category.CategoryName).FirstOrDefault() ?? string.Empty,
                    DetectedAt = m.CapturedAt
                }).ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<MissionStatusOverviewModel> GetMissionStatusOverviewAsync(CancellationToken cancellationToken)
        {
            var pending = await _context.Missions.CountAsync(m => m.Status == "Scheduled", cancellationToken);
            var inProgress = await _context.Missions.CountAsync(m => m.Status == "InProgress", cancellationToken);
            var completed = await _context.Missions.CountAsync(m => m.Status == "Completed", cancellationToken);

            return new MissionStatusOverviewModel
            {
                Pending = pending,
                InProgress = inProgress,
                Completed = completed
            };
        }

        public async Task<(List<RecentDefectModel> Items, int TotalCount)> GetRecentDefectsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = _context.DetectedAnomalies
                .Where(a => a.ValidationStatus == "Confirmed");
            
            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new RecentDefectModel
                {
                    InspectionId = a.MediaId,
                    MissionId = a.Media.MissionId,
                    MissionTitle = !string.IsNullOrEmpty(a.Media.Mission.Description) 
                    ? a.Media.Mission.Description : a.Media.Mission.MissionCode,
                    ImageUrl = a.Media.FileUrl,
                    DefectType = a.Category.CategoryName,
                    DetectedAt = a.CreatedAt
                }).ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<MonitorSummaryModel> GetSummaryAsync(CancellationToken cancellationToken)
        {
            var totalMissions = await _context.Missions.CountAsync(cancellationToken);
            var pendingMissions = await _context.Missions.CountAsync(m => m.Status == "Scheduled", cancellationToken);
            var inProgressMissions = await _context.Missions.CountAsync(m => m.Status == "InProgress", cancellationToken);
            var completedMissions = await _context.Missions.CountAsync(m => m.Status == "Completed", cancellationToken);
           
            var totalInspections = await _context.InspectionMedia.CountAsync(cancellationToken);
            
            var totalDefects = await _context.DetectedAnomalies.CountAsync(a => a.ValidationStatus == "Confirmed", cancellationToken);

            var criricalDefects = await _context.DetectedAnomalies.CountAsync(a => a.ValidationStatus == "Confirmed" && a.Category.IsEmergencyClass, cancellationToken);


            return new MonitorSummaryModel
            {
                TotalMissions = totalMissions,
                PendingMissions = pendingMissions,
                InProgressMissions = inProgressMissions,
                CompletedMissions = completedMissions,
                TotalInspections = totalInspections,
                TotalDefects = totalDefects,
                CriticalDefects = criricalDefects
            };
        }
    }
}
