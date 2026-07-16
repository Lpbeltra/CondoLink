FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CondoLink.sln ./
COPY CondoLink.Api/CondoLink.Api.csproj CondoLink.Api/
COPY CondoLink.Domain/CondoLink.Domain.csproj CondoLink.Domain/
COPY CondoLink.Infrastructure/CondoLink.Infrastructure.csproj CondoLink.Infrastructure/
RUN dotnet restore CondoLink.sln

COPY . .
RUN dotnet publish CondoLink.Api/CondoLink.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CondoLink.Api.dll"]
