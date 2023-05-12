using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateways.Core.DataAccess;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.DataTransferObjects;


namespace PaymentGateways.Api.Installers
{
    public class DbInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
            services.AddScoped<IDataLayer, DataLayer>();
        }
    }
}