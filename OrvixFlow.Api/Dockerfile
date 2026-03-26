FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["OrvixFlow.sln", "./"]
COPY ["OrvixFlow.Api/OrvixFlow.Api.csproj", "OrvixFlow.Api/"]
COPY ["OrvixFlow.Core/OrvixFlow.Core.csproj", "OrvixFlow.Core/"]
COPY ["OrvixFlow.Infrastructure/OrvixFlow.Infrastructure.csproj", "OrvixFlow.Infrastructure/"]
RUN dotnet restore "OrvixFlow.Api/OrvixFlow.Api.csproj"

COPY . .
WORKDIR "/src/OrvixFlow.Api"
RUN dotnet publish "OrvixFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrvixFlow.Api.dll"]
