# StaySphere API — production-style multi-stage build.
# Build context: the Backend/ directory (see Backend/.dockerignore).
#
#   docker build -t staysphere-api:local Backend
#
# Target framework is net10.0 (all projects), so we use the .NET 10 SDK to
# build and the ASP.NET Core 10 runtime to run.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only the project files, so the restore layer is cached
# until a .csproj actually changes. Restoring the API project transitively
# restores Application, Infrastructure and Domain; the test project is not needed.
COPY StaySphere.Api/StaySphere.Api.csproj                       StaySphere.Api/
COPY StaySphere.Application/StaySphere.Application.csproj         StaySphere.Application/
COPY StaySphere.Domain/StaySphere.Domain.csproj                  StaySphere.Domain/
COPY StaySphere.Infrastructure/StaySphere.Infrastructure.csproj  StaySphere.Infrastructure/
RUN dotnet restore StaySphere.Api/StaySphere.Api.csproj

# Copy the remaining sources and publish. UseAppHost=false drops the unused
# native launcher; the app is started with `dotnet StaySphere.Api.dll`.
COPY . .
RUN dotnet publish StaySphere.Api/StaySphere.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Kestrel listens on HTTP 8080 (the aspnet image default; set explicitly here so
# it is obvious and overridable). No HTTPS inside the container — TLS terminates
# upstream.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Persistent SQLite lives here; a volume is mounted over it at run time. Create
# it now, owned by the image's non-root user, so named volumes inherit correct
# ownership. APP_UID / the `app` user are provided by the aspnet base image.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
USER $APP_UID

# The connection string, CORS origins and environment are supplied at run time,
# not baked in. The app reads ConnectionStrings:StaySphere
# (env: ConnectionStrings__StaySphere).
ENTRYPOINT ["dotnet", "StaySphere.Api.dll"]
