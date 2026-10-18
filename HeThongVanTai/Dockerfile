# Image API (ASP.NET Core 8). Build từ thư mục gốc của dự án:
#   docker build -f HeThongVanTai/Dockerfile -t vantai-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Chép riêng file csproj và restore trước để tận dụng cache khi chỉ sửa code
COPY HeThongVanTai/HeThongVanTai.csproj HeThongVanTai/
RUN dotnet restore HeThongVanTai/HeThongVanTai.csproj
COPY HeThongVanTai/ HeThongVanTai/
RUN dotnet publish HeThongVanTai/HeThongVanTai.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# curl dùng cho HEALTHCHECK
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
# Thư mục cần ghi: log, ảnh tải lên, khóa mã hóa. Chạy bằng người dùng thường (app), không dùng root.
RUN mkdir -p /app/Logs /app/wwwroot/Images /keys && chown -R app:app /app/Logs /app/wwwroot /keys
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "HeThongVanTai.dll"]
