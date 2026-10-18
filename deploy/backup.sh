#!/usr/bin/env bash
# Sao lưu hai CSDL (VanTaiDB, VanTaiAuthDB) và thư mục ảnh vào backups/<thời-điểm>/.
# Chạy từ bất kỳ đâu:  bash deploy/backup.sh      (cần compose đang chạy; đặt cron để chạy hằng ngày)
set -euo pipefail
cd "$(dirname "$0")/.."

STAMP=$(date +%Y%m%d_%H%M%S)
OUT="backups/$STAMP"
KEEP_DAYS="${KEEP_DAYS:-14}"
mkdir -p "$OUT"

# Chạy T-SQL trong container db (mật khẩu lấy từ biến môi trường của container, không hiện trên dòng lệnh)
run_sql() {
  printf '%s\nGO\n' "$1" | docker compose exec -T db bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b'
}

docker compose exec -T db mkdir -p /var/opt/mssql/backup

for DB in VanTaiDB VanTaiAuthDB; do
  FILE="/var/opt/mssql/backup/$STAMP-$DB.bak"
  echo "Sao lưu $DB ..."
  run_sql "BACKUP DATABASE [$DB] TO DISK = N'$FILE' WITH COMPRESSION, CHECKSUM, INIT, NAME = N'$DB $STAMP'"
  run_sql "RESTORE VERIFYONLY FROM DISK = N'$FILE' WITH CHECKSUM"
  docker compose cp "db:$FILE" "$OUT/$DB.bak"
  docker compose exec -T db rm -f "$FILE"
done

echo "Sao lưu thư mục ảnh ..."
docker compose exec -T api tar -czf /tmp/images.tgz -C /app/wwwroot Images
docker compose cp api:/tmp/images.tgz "$OUT/images.tar.gz"
docker compose exec -T api rm -f /tmp/images.tgz

# Xóa các bản sao lưu cũ hơn KEEP_DAYS ngày (mặc định 14)
find backups -mindepth 1 -maxdepth 1 -type d -mtime +"$KEEP_DAYS" -exec rm -rf {} + 2>/dev/null || true

echo "Xong: $OUT"
ls -lh "$OUT"
echo "Hãy chép thư mục này sang nơi khác (máy khác, ổ đĩa ngoài hoặc dịch vụ lưu trữ). Sao lưu chỉ nằm trên cùng máy chủ thì không an toàn."
