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