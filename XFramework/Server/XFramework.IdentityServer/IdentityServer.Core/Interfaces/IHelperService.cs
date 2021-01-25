namespace IdentityServer.Core.Interfaces
{
    public interface IHelperService
    {
        public string GenerateRandomString(int size);
        public string GenerateToken();
    }
}