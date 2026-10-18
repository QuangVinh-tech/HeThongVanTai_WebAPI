#!/usr/bin/env bash
# Phục hồi hai CSDL và ảnh từ một thư mục sao lưu:  bash deploy/restore.sh backups/20261018_020000
# CẢNH BÁO: ghi đè toàn bộ dữ liệu hiện tại. Web và API được dừng trong lúc phục hồi.
set -euo pipefail
cd "$(dirname "$0")/.."

SRC="${1:?Cách dùng: bash deploy/restore.sh backups/<thời-điểm>}"
if [ ! -f "$SRC/VanTaiDB.bak" ] || [ ! -f "$SRC/VanTaiAuthDB.bak" ]; then
  echo "Thiếu VanTaiDB.bak hoặc VanTaiAuthDB.bak trong $SRC"
  exit 1
fi

read -r -p "Phục hồi sẽ GHI ĐÈ dữ liệu hiện tại bằng bản sao lưu trong $SRC. Gõ CO để tiếp tục: " OK
if [ "$OK" != "CO" ]; then echo "Đã hủy."; exit 1; fi

run_sql() {
  printf '%s\nGO\n' "$1" | docker compose exec -T db bash -c 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b'
}

echo "Dừng web, api, caddy ..."
docker compose stop caddy web api
docker compose exec -T db mkdir -p /var/opt/mssql/backup

for DB in VanTaiAuthDB VanTaiDB; do
  FILE="/var/opt/mssql/backup/restore-$DB.bak"
  echo "Phục hồi $DB ..."
  docker compose cp "$SRC/$DB.bak" "db:$FILE"
  run_sql "RESTORE VERIFYONLY FROM DISK = N'$FILE' WITH CHECKSUM"
  run_sql "IF DB_ID(N'$DB') IS NOT NULL ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$DB] FROM DISK = N'$FILE' WITH REPLACE, CHECKSUM, RECOVERY; ALTER DATABASE [$DB] SET MULTI_USER;"
  docker compose exec -T db rm -f "$FILE"
done

echo "Khởi động lại hệ thống ..."
docker compose up -d

if [ -f "$SRC/images.tar.gz" ]; then
  echo "Phục hồi thư mục ảnh ..."
  # Đợi API sẵn sàng rồi giải nén ảnh
  for i in $(seq 1 30); do
    if docker compose exec -T api curl -fsS http://localhost:8080/health >/dev/null 2>&1; then break; fi
    sleep 3
  done
  docker compose cp "$SRC/images.tar.gz" api:/tmp/images.tgz
  docker compose exec -T api tar -xzf /tmp/images.tgz -C /app/wwwroot
fi

echo "Xong. Kiểm tra: docker compose ps  và mở trang /health."
