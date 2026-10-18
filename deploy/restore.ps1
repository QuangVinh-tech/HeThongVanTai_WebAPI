# Phục hồi hai CSDL và ảnh từ một thư mục sao lưu (Windows PowerShell).
# Chạy:  powershell -ExecutionPolicy Bypass -File deploy\restore.ps1 backups\20261018_020000
# CẢNH BÁO: ghi đè toàn bộ dữ liệu hiện tại. Web và API được dừng trong lúc phục hồi.
param([Parameter(Mandatory = $true)][string]$Source)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Test-Path (Join-Path $Source 'VanTaiDB.bak')) -or -not (Test-Path (Join-Path $Source 'VanTaiAuthDB.bak'))) {
    throw "Thiếu VanTaiDB.bak hoặc VanTaiAuthDB.bak trong $Source"
}

$ok = Read-Host "Phục hồi sẽ GHI ĐÈ dữ liệu hiện tại bằng bản sao lưu trong $Source. Gõ CO để tiếp tục"
if ($ok -ne 'CO') { Write-Host 'Đã hủy.'; exit 1 }

function Invoke-Sql([string]$sql) {
    "$sql`nGO`n" | docker compose exec -T db bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b'
    if ($LASTEXITCODE -ne 0) { throw "Lệnh SQL bị lỗi: $sql" }
}

Write-Host 'Dừng web, api, caddy ...'
docker compose stop caddy web api
docker compose exec -T db mkdir -p /var/opt/mssql/backup

foreach ($db in 'VanTaiAuthDB', 'VanTaiDB') {
    $file = "/var/opt/mssql/backup/restore-$db.bak"
    Write-Host "Phục hồi $db ..."
    docker compose cp (Join-Path $Source "$db.bak") "db:$file"
    if ($LASTEXITCODE -ne 0) { throw "Không chép được file sao lưu của $db vào container" }
    Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM"
    Invoke-Sql "IF DB_ID(N'$db') IS NOT NULL ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$db] FROM DISK = N'$file' WITH REPLACE, CHECKSUM, RECOVERY; ALTER DATABASE [$db] SET MULTI_USER;"
    docker compose exec -T db rm -f $file
}

Write-Host 'Khởi động lại hệ thống ...'
docker compose up -d

$images = Join-Path $Source 'images.tar.gz'
if (Test-Path $images) {
    Write-Host 'Phục hồi thư mục ảnh ...'
    for ($i = 0; $i -lt 30; $i++) {
        docker compose exec -T api curl -fsS http://localhost:8080/health *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 3
    }
    docker compose cp $images api:/tmp/images.tgz
    docker compose exec -T api tar -xzf /tmp/images.tgz -C /app/wwwroot
}

Write-Host 'Xong. Kiểm tra: docker compose ps  và mở trang /health.'
