FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY CondoLink.Api/CondoLink.Api.csproj CondoLink.Api/
COPY CondoLink.Domain/CondoLink.Domain.csproj CondoLink.Domain/
COPY CondoLink.Infrastructure/CondoLink.Infrastructure.csproj CondoLink.Infrastructure/

RUN dotnet restore CondoLink.Api/CondoLink.Api.csproj

COPY . .

RUN dotnet publish CondoLink.Api/CondoLink.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "CondoLink.Api.dll"]