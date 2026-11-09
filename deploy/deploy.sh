#!/usr/bin/env bash
# =============================================================================
#  Triển khai Culinary Blog lên máy chủ Ubuntu 24.04 (Azure for Students, Oracle, VPS) — Tuần 10 (Quang)
#
#  Chạy trong thư mục repo trên máy ảo:
#      bash deploy/deploy.sh
#  Dùng domain riêng thay cho <IP>.sslip.io:
#      SITE_DOMAIN=blog.vidu.com bash deploy/deploy.sh
#
#  Script:
#    1. Cài Docker nếu chưa có.
#    2. Máy RAM < 6 GB (vd Azure B2ats_v2 1 GB): tạo 4 GB swap để build .NET + Next.js không hết bộ nhớ.
#       Mở cổng 80/443 trong iptables (ảnh Ubuntu của Oracle chặn sẵn). Cổng trên web (Azure NSG /
#       Oracle Security List) vẫn phải mở tay.
#    3. Lần đầu: tạo .env.server với mật khẩu/khoá NGẪU NHIÊN (chmod 600, không commit).
#    4. Build + chạy toàn bộ bằng docker-compose.prod.yml + docker-compose.server.yml.
#    5. Chờ HTTPS sẵn sàng rồi in địa chỉ web và tài khoản Admin.
#  Chạy lại script để cập nhật sau khi git pull (giữ nguyên .env.server và dữ liệu).
# =============================================================================
set -euo pipefail

cd "$(dirname "$0")/.."
ENV_FILE=.env.server
COMPOSE_FILES=(-f docker-compose.prod.yml -f docker-compose.server.yml)

# --- 1. Docker ---------------------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
  echo ">> Cài Docker..."
  curl -fsSL https://get.docker.com | sudo sh
  sudo usermod -aG docker "$USER" || true
fi
DOCKER=(docker)
if ! docker info >/dev/null 2>&1; then DOCKER=(sudo docker); fi

# --- 2a. Swap cho máy ít RAM ---------------------------------------------------
MEM_MB=$(awk '/MemTotal/ {print int($2/1024)}' /proc/meminfo)
if [[ "$MEM_MB" -lt 6000 && -z "$(swapon --show --noheadings)" ]]; then
  echo ">> RAM ${MEM_MB} MB — tạo 4 GB swap (/swapfile)..."
  sudo fallocate -l 4G /swapfile
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile >/dev/null
  sudo swapon /swapfile
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
fi

# --- 2b. Firewall trong máy ảo (ảnh Ubuntu của Oracle chặn mọi cổng trừ 22) -------------
if command -v iptables >/dev/null 2>&1; then
  for rule in "tcp 80" "tcp 443" "udp 443"; do
    set -- $rule
    sudo iptables -C INPUT -p "$1" --dport "$2" -j ACCEPT 2>/dev/null \
      || sudo iptables -I INPUT 1 -p "$1" --dport "$2" -j ACCEPT
  done
  if command -v netfilter-persistent >/dev/null 2>&1; then sudo netfilter-persistent save >/dev/null 2>&1 || true; fi
fi

# --- 3. .env.server ----------------------------------------------------------
if [[ ! -f "$ENV_FILE" ]]; then
  PUBLIC_IP=$(curl -fsS --max-time 10 https://api.ipify.org || curl -fsS --max-time 10 https://ifconfig.me)
  DOMAIN=${SITE_DOMAIN:-${PUBLIC_IP//./-}.sslip.io}
  echo ">> IP công khai: $PUBLIC_IP  →  địa chỉ web: https://$DOMAIN"

  read -r -p "Email Admin (đăng nhập web + nhận thông báo chứng chỉ HTTPS): " ADMIN_EMAIL
  [[ "$ADMIN_EMAIL" == *@* ]] || { echo "Email không hợp lệ"; exit 1; }
  read -r -p "SMTP Gmail để gửi email (Enter để bỏ qua): " SMTP_USER
  SMTP_PASS=""
  if [[ -n "$SMTP_USER" ]]; then read -r -s -p "App Password 16 ký tự của Gmail: " SMTP_PASS; echo; fi

  rand() { openssl rand -hex "$1"; }
  ADMIN_PASSWORD="Cb@$(rand 6)X9"
  umask 077
  cat > "$ENV_FILE" <<ENV
# Tạo bởi deploy/deploy.sh — KHÔNG commit, KHÔNG gửi file này cho ai.
SITE_DOMAIN=$DOMAIN
ACME_EMAIL=$ADMIN_EMAIL
PUBLIC_WEB_URL=https://$DOMAIN
PUBLIC_API_URL=https://$DOMAIN/api/v1
PUBLIC_STORAGE_URL=https://$DOMAIN

POSTGRES_PASSWORD=$(rand 24)
REDIS_PASSWORD=$(rand 24)
MINIO_ROOT_USER=culinaryadmin
MINIO_ROOT_PASSWORD=$(rand 24)
SEQ_ADMIN_PASSWORD=$(rand 16)
JWT_SECRET=$(rand 32)
AUTH_SECRET=$(rand 32)

ADMIN_EMAIL=$ADMIN_EMAIL
ADMIN_PASSWORD=$ADMIN_PASSWORD

SMTP_HOST=${SMTP_USER:+smtp.gmail.com}
SMTP_PORT=587
SMTP_SECURITY=StartTls
SMTP_USERNAME=$SMTP_USER
SMTP_PASSWORD=$SMTP_PASS
SMTP_FROM_EMAIL=${SMTP_USER:-$ADMIN_EMAIL}

GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
ENV
  # Không có SMTP: email chào mừng sẽ lỗi trong Hangfire (tự thử lại), web vẫn chạy bình thường.
  sed -i 's/^SMTP_HOST=$/SMTP_HOST=localhost/' "$ENV_FILE"
  chmod 600 "$ENV_FILE"
  echo ">> Đã tạo $ENV_FILE"
fi

DOMAIN=$(grep '^SITE_DOMAIN=' "$ENV_FILE" | cut -d= -f2-)

# --- 4. Build + chạy ---------------------------------------------------------
echo ">> Build và khởi động (lần đầu mất khoảng 10-20 phút; máy 1 GiB RAM có thể tới 40 phút)..."
if [[ "$MEM_MB" -lt 3000 ]]; then
  # Ít RAM: build TỪNG image một (.NET rồi Next.js) thay vì song song, tránh hết bộ nhớ.
  "${DOCKER[@]}" compose "${COMPOSE_FILES[@]}" --env-file "$ENV_FILE" build api
  "${DOCKER[@]}" compose "${COMPOSE_FILES[@]}" --env-file "$ENV_FILE" build frontend
fi
"${DOCKER[@]}" compose "${COMPOSE_FILES[@]}" --env-file "$ENV_FILE" up -d --build --remove-orphans

# --- 5. Chờ HTTPS ------------------------------------------------------------
echo ">> Chờ chứng chỉ HTTPS và API sẵn sàng..."
for i in $(seq 1 60); do
  if curl -fsS --max-time 5 "https://$DOMAIN/health/ready" >/dev/null 2>&1; then
    echo
    echo "=============================================================="
    echo " Web đã chạy:  https://$DOMAIN"
    echo " Admin:        $(grep '^ADMIN_EMAIL=' "$ENV_FILE" | cut -d= -f2-)"
    echo " Mật khẩu:     xem bằng lệnh  grep ADMIN_PASSWORD $ENV_FILE"
    echo " Log:          ${DOCKER[*]} compose ${COMPOSE_FILES[*]} --env-file $ENV_FILE logs -f api"
    echo "=============================================================="
    exit 0
  fi
  sleep 10
done

echo "!! Sau 10 phút chưa truy cập được https://$DOMAIN/health/ready"
echo "   Kiểm tra: đã mở cổng 80 và 443 (Azure: Networking/NSG; Oracle: Security List) chưa; xem log Caddy:"
echo "   ${DOCKER[*]} compose ${COMPOSE_FILES[*]} --env-file $ENV_FILE logs caddy"
exit 1
