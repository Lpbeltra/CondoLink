FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY CondoLink.Api/CondoLink.Api.csproj CondoLink.Api/
COPY CondoLink.Domain/CondoLink.Domain.csproj CondoLink.Domain/
COPY CondoLink.Infrastructure/CondoLink.Infrastructure.csproj CondoLink.Infrastructure/
COPY CondoLink.DataReset/CondoLink.DataReset.csproj CondoLink.DataReset/

RUN dotnet restore CondoLink.Api/CondoLink.Api.csproj
RUN dotnet restore CondoLink.DataReset/CondoLink.DataReset.csproj

COPY . .

RUN dotnet publish CondoLink.Api/CondoLink.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

RUN dotnet publish CondoLink.DataReset/CondoLink.DataReset.csproj \
    --configuration Release \
    --output /app/datareset \
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

ENV FileStorage__RootPath=/app/data/attachments

COPY --from=build /app/publish .
COPY --from=build /app/datareset /app/datareset

RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-liberation2 \
    && rm -rf /var/lib/apt/lists/* \
    && chmod +x /app/efbundle \
    && mkdir -p /app/data/attachments

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "./efbundle && exec dotnet CondoLink.Api.dll"]
