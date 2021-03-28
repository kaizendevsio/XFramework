using System.Threading.Tasks;

namespace XFramework.Identity.Shared.Core.Interfaces
{
    public interface IActivityHistoryService
    {
        public void AddPageToHistory(string pageName);
        public bool CanGoBack();
        public Task GoBack();
    }
}