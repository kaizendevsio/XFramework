dotnet ef dbcontext scaffold "Host=localhost;Database=XnelSystems;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY" Npgsql.EntityFrameworkCore.PostgreSQL -o DataTransferObjects -f --project ../XFramework.Domain.csproj --schema "Application" --schema "Identity" --schema "GeoLocation" --schema "Finance" --schema "Audit" --schema "Registry" --schema "Wallet" --schema "UserData"

pause