# Image Web (ASP.NET Core MVC 8). Build từ thư mục gốc của dự án:
#   docker build -f HeThongVanTai.Web/Dockerfile -t vantai-web .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY HeThongVanTai.Web/HeThongVanTai.Web.csproj HeThongVanTai.Web/
RUN dotnet restore HeThongVanTai.Web/HeThongVanTai.Web.csproj
COPY HeThongVanTai.Web/ HeThongVanTai.Web/
RUN dotnet publish HeThongVanTai.Web/HeThongVanTai.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/Logs /keys && chown -R app:app /app/Logs /keys
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "HeThongVanTai.Web.dll"]
