# Sao lưu hai CSDL và thư mục ảnh vào backups\<thời-điểm>\ (Windows PowerShell).
# Chạy:  powershell -ExecutionPolicy Bypass -File deploy\backup.ps1      (cần compose đang chạy)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$out = Join-Path 'backups' $stamp
New-Item -ItemType Directory -Force -Path $out | Out-Null
$keepDays = if ($env:KEEP_DAYS) { [int]$env:KEEP_DAYS } else { 14 }

# Chạy T-SQL trong container db (mật khẩu lấy từ biến môi trường của container)
function Invoke-Sql([string]$sql) {
    "$sql`nGO`n" | docker compose exec -T db bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b'
    if ($LASTEXITCODE -ne 0) { throw "Lệnh SQL bị lỗi: $sql" }
}

docker compose exec -T db mkdir -p /var/opt/mssql/backup

foreach ($db in 'VanTaiDB', 'VanTaiAuthDB') {
    $file = "/var/opt/mssql/backup/$stamp-$db.bak"
    Write-Host "Sao lưu $db ..."
    Invoke-Sql "BACKUP DATABASE [$db] TO DISK = N'$file' WITH COMPRESSION, CHECKSUM, INIT, NAME = N'$db $stamp'"
    Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM"
    docker compose cp "db:$file" (Join-Path $out "$db.bak")
    if ($LASTEXITCODE -ne 0) { throw "Không chép được file sao lưu của $db" }
    docker compose exec -T db rm -f $file
}

Write-Host 'Sao lưu thư mục ảnh ...'
docker compose exec -T api tar -czf /tmp/images.tgz -C /app/wwwroot Images
docker compose cp api:/tmp/images.tgz (Join-Path $out 'images.tar.gz')
docker compose exec -T api rm -f /tmp/images.tgz

# Xóa bản sao lưu cũ hơn $keepDays ngày
Get-ChildItem backups -Directory | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$keepDays) } | Remove-Item -Recurse -Force

Write-Host "Xong: $out"
Get-ChildItem $out | Format-Table Name, Length
Write-Host 'Hãy chép thư mục này sang nơi khác (máy khác, ổ đĩa ngoài hoặc dịch vụ lưu trữ).'
