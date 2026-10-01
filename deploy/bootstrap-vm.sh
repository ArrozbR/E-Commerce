set -euo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Rode com sudo: sudo bash $0" >&2
  exit 1
fi

log() { echo -e "\n==> $*"; }

SWAPFILE=/swapfile

if swapon --show=NAME --noheadings | grep -qx "$SWAPFILE"; then
  log "Swap já está ativo, nada a fazer"
else
  log "Criando swap de 2 GB em $SWAPFILE"
  fallocate -l 2G "$SWAPFILE"
  chmod 600 "$SWAPFILE"
  mkswap "$SWAPFILE"
  swapon "$SWAPFILE"
fi

grep -q "^$SWAPFILE " /etc/fstab || echo "$SWAPFILE none swap sw 0 0" >> /etc/fstab

echo "vm.swappiness=10" > /etc/sysctl.d/99-keycapstore.conf
sysctl --quiet --load /etc/sysctl.d/99-keycapstore.conf

log "Parte 1 concluída"
free -h

export DEBIAN_FRONTEND=noninteractive

log "Definindo o fuso horário para America/Sao_Paulo"
timedatectl set-timezone America/Sao_Paulo

log "Atualizando a lista de pacotes e o sistema"
apt-get update -q
apt-get upgrade -y -q

log "Instalando/garantindo o unattended-upgrades"
apt-get install -y -q unattended-upgrades

cat > /etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF

cat > /etc/apt/apt.conf.d/52keycapstore-unattended <<'EOF'
// Reinicia sozinho quando uma atualização exigir (ex.: kernel)...
Unattended-Upgrade::Automatic-Reboot "true";
// ...às 04:00 no horário de Brasília (o fuso da VM, definido acima).
Unattended-Upgrade::Automatic-Reboot-Time "04:00";
// Remove dependências que ficaram sem uso (economiza disco).
Unattended-Upgrade::Remove-Unused-Dependencies "true";
EOF

log "Parte 2 concluída"

log "Configurando o repositório oficial da Docker"
apt-get install -y -q ca-certificates curl

install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF

log "Instalando/garantindo o Docker Engine e o Compose"
apt-get update -q
apt-get install -y -q docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

cat > /tmp/daemon.json <<'EOF'
{
  "log-driver": "json-file",
  "log-opts": { "max-size": "10m", "max-file": "3" }
}
EOF
if ! cmp -s /tmp/daemon.json /etc/docker/daemon.json; then
  log "Aplicando a rotação de logs do Docker"
  mv /tmp/daemon.json /etc/docker/daemon.json
  systemctl restart docker
else
  rm /tmp/daemon.json
fi

systemctl enable --now docker

log "Parte 3 concluída"
docker --version
docker compose version

log "Liberando as portas 80 e 443 no iptables"
for PORT in 80 443; do
  if ! iptables -C INPUT -p tcp -m state --state NEW -m tcp --dport "$PORT" -j ACCEPT 2>/dev/null; then
    REJECT_POS=$(iptables -L INPUT --line-numbers -n | awk '$2 == "REJECT" { print $1; exit }')
    iptables -I INPUT "$REJECT_POS" -p tcp -m state --state NEW -m tcp --dport "$PORT" -j ACCEPT
  fi

  RULE="-A INPUT -p tcp -m state --state NEW -m tcp --dport $PORT -j ACCEPT"
  if ! grep -qxF -- "$RULE" /etc/iptables/rules.v4; then
    sed -i "/^-A INPUT -j REJECT/i $RULE" /etc/iptables/rules.v4
  fi
done
log "Endurecendo o SSH"
SSHD_CONF=/etc/ssh/sshd_config.d/01-keycapstore.conf
cat > /tmp/01-keycapstore.conf <<'EOF'
# KeycapStore — ADR 0017: login só por chave; root não entra por SSH.
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
EOF
if ! cmp -s /tmp/01-keycapstore.conf "$SSHD_CONF"; then
  mv /tmp/01-keycapstore.conf "$SSHD_CONF"
  if ! sshd -t; then
    rm -f "$SSHD_CONF"
    echo "ERRO: configuração do SSH inválida; mudança desfeita." >&2
    exit 1
  fi
  systemctl reload ssh
else
  rm /tmp/01-keycapstore.conf
fi

log "Instalando/garantindo o fail2ban"
apt-get install -y -q fail2ban
systemctl enable --now fail2ban

log "Parte 4 concluída"

log "Instalando/garantindo o Nginx"
apt-get install -y -q nginx
systemctl enable --now nginx

log "Parte 5 (Nginx) concluída"

DOMAIN=keycapstore.duckdns.org
SITE_CONF=/etc/nginx/sites-available/keycapstore
CERT_DIR=/etc/letsencrypt/live/$DOMAIN

log "Instalando/garantindo o Certbot"
apt-get install -y -q certbot python3-certbot-nginx

rm -f /etc/nginx/sites-enabled/default
ln -sf "$SITE_CONF" /etc/nginx/sites-enabled/keycapstore

if [[ ! -f "$CERT_DIR/fullchain.pem" ]]; then
  : "${LETSENCRYPT_EMAIL:?Rode assim: sudo LETSENCRYPT_EMAIL=seu@email bash bootstrap-vm.sh}"

  log "Configuração temporária só na porta 80, para o Let's Encrypt validar o domínio"
  cat > "$SITE_CONF" <<EOF
server {
    listen 80;
    server_name $DOMAIN;
    location / { return 200 "KeycapStore: emitindo certificado"; }
}
EOF
  nginx -t && systemctl reload nginx

  log "Pedindo o certificado HTTPS para $DOMAIN"
  certbot certonly --nginx -d "$DOMAIN" --non-interactive --agree-tos \
    -m "$LETSENCRYPT_EMAIL" --deploy-hook "systemctl reload nginx"
fi

log "Configuração definitiva do Nginx (HTTPS + encaminhamento para o site)"
cat > "$SITE_CONF" <<EOF
# Porta 80 (HTTP, sem cadeado): manda todo mundo para a versão HTTPS.
server {
    listen 80;
    server_name $DOMAIN;
    server_tokens off;   # não anuncia a versão do Nginx
    location / { return 301 https://\$host\$request_uri; }
}

# Porta 443 (HTTPS, com cadeado): a "recepção" que encaminha para o site.
server {
    listen 443 ssl;
    server_name $DOMAIN;
    server_tokens off;

    ssl_certificate     $CERT_DIR/fullchain.pem;
    ssl_certificate_key $CERT_DIR/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    location / {
        proxy_pass http://127.0.0.1:8080;   # o container do site (só acessível de dentro da VM)
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF

nginx -t
systemctl reload nginx

log "Parte 5b concluída: https://$DOMAIN"

DEPLOY_USER=deploy
APP_DIR=/opt/keycapstore

if ! id "$DEPLOY_USER" >/dev/null 2>&1; then
  log "Criando o usuário $DEPLOY_USER"
  useradd --create-home --shell /bin/bash "$DEPLOY_USER"
fi

SCRIPT_DIR=$(dirname "$(readlink -f "$0")")
install -d -m 755 "$APP_DIR"
install -m 750 -o root -g root "$SCRIPT_DIR/deploy.sh" "$APP_DIR/deploy.sh"

log "Permitindo ao $DEPLOY_USER rodar só o deploy.sh com sudo"
echo "$DEPLOY_USER ALL=(root) NOPASSWD: $APP_DIR/deploy.sh" > /tmp/keycapstore-deploy
visudo -cf /tmp/keycapstore-deploy
install -m 440 /tmp/keycapstore-deploy /etc/sudoers.d/keycapstore-deploy
rm /tmp/keycapstore-deploy

if [[ -n "${DEPLOY_PUBKEY:-}" ]]; then
  log "Cadastrando a chave do GitHub com comando forçado"
  install -d -m 700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh"
  printf 'command="sudo %s/deploy.sh \\"$SSH_ORIGINAL_COMMAND\\"",restrict %s\n' \
    "$APP_DIR" "$DEPLOY_PUBKEY" > "/home/$DEPLOY_USER/.ssh/authorized_keys"
  chown "$DEPLOY_USER:$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh/authorized_keys"
  chmod 600 "/home/$DEPLOY_USER/.ssh/authorized_keys"
elif [[ ! -f "/home/$DEPLOY_USER/.ssh/authorized_keys" ]]; then
  echo "AVISO: nenhuma chave de deploy cadastrada. Rode com DEPLOY_PUBKEY=\"...\"" >&2
fi

log "Parte 6 concluída"