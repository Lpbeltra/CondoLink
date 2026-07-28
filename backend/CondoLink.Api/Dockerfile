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

# Instala a ferramenta do Entity Framework no estágio de build
RUN dotnet tool install --global dotnet-ef --version 10.*

ENV PATH="${PATH}:/root/.dotnet/tools"

# A string é apenas para o EF conseguir criar o DbContext durante o build.
# Nenhuma conexão com esse banco fictício será realizada.
RUN ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=condolink;Username=condolink;Password=build-only" \
    dotnet ef migrations bundle \
    --project CondoLink.Infrastructure/CondoLink.Infrastructure.csproj \
    --startup-project CondoLink.Api/CondoLink.Api.csproj \
    --configuration Release \
    --output /app/publish/efbundle \
    --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

RUN chmod +x /app/efbundle

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "./efbundle && exec dotnet CondoLink.Api.dll"]