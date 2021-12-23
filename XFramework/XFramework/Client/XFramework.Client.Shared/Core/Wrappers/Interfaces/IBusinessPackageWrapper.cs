namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IBusinessPackageWrapper
    {
        public Task<CmdResponseBO> Create();
        public Task<CmdResponseBO> Get();
        public Task<CmdResponseBO> GetList();
        public Task<CmdResponseBO> Update();
        public Task<CmdResponseBO> Delete();
        public Task<CmdResponseBO> Purchase();
        public Task<CmdResponseBO> Cancel();
    }
}