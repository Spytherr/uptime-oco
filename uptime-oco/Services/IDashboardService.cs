namespace uptime_oco;

public interface IDashboardService
{
    Task<ServiceResult<DashboardViewModel>> GetDashboardDataAsync();
}
