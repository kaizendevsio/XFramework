namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IELoadWrapper
    {
        public Task<List<TelcoEntityContract>> GetTelcoList();
        public Task<CmdResponseBO> Transact(ELoadTransactRequest request);

    }
}