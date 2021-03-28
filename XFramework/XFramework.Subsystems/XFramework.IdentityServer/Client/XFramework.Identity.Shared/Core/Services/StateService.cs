using XFramework.Identity.Shared.Core.Interfaces;

namespace XFramework.Identity.Shared.Core.Services
{
    public class StateService : IStateService
    {
        public StateService(IActivityHistoryService activityHistoryService)
        {
            ActivityHistory = activityHistoryService;
        }
        public IActivityHistoryService ActivityHistory { get; set; }
    }
}