dotnet ef dbcontext scaffold "Host=localhost;Database=XFramework;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY" Npgsql.EntityFrameworkCore.PostgreSQL -o DTO -f --project ../IdentityServer.Domain.csproj --schema "Application" --schema "Identity" --schema "GeoLocation" --schema "Finance"

pause