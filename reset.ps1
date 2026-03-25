$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef database drop -f --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
dotnet ef database update --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
