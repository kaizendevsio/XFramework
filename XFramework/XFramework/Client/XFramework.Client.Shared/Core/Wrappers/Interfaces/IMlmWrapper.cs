namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IMlmWrapper
    {
        public Task<CmdResponseBO> GetCommissionHistory();
        public Task<CmdResponseBO> GetCommissionSummary();
        public Task<CmdResponseBO> GetBinaryMap();
    }
}