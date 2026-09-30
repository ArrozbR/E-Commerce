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