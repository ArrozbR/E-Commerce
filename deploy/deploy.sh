#!/usr/bin/env bash
# deploy.sh — coloca no ar uma versão (imagem) da loja.
# Uso (na VM):  sudo /opt/keycapstore/deploy.sh <hash-do-commit>
set -euo pipefail
cd /opt/keycapstore

IMAGE=ghcr.io/arrozbr/keycapstore-web
NEW_TAG="${1:-}"

if [[ ! "$NEW_TAG" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Uso: sudo $0 <hash do commit, 40 caracteres>" >&2
  exit 1
fi

OLD_TAG=$(grep '^APP_TAG=' .env | cut -d= -f2)
echo "==> Versão atual: $OLD_TAG"
echo "==> Nova versão:  $NEW_TAG"

docker pull "$IMAGE:$NEW_TAG"

RAW_URL="https://raw.githubusercontent.com/ArrozbR/E-Commerce/$NEW_TAG/deploy"
TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT
curl -fsSL "$RAW_URL/compose.prod.yaml" -o "$TMP_DIR/compose.yaml"
curl -fsSL "$RAW_URL/deploy.sh" -o "$TMP_DIR/deploy.sh"
install -m 640 "$TMP_DIR/compose.yaml" compose.yaml

APP_TAG="$NEW_TAG" docker compose run --rm -T app migrate

sed -i "s/^APP_TAG=.*/APP_TAG=$NEW_TAG/" .env

docker compose up -d app

for i in $(seq 1 30); do
  if curl -fsS -o /dev/null http://127.0.0.1:8080/; then
    install -m 750 "$TMP_DIR/deploy.sh" deploy.sh.new
    mv deploy.sh.new deploy.sh
    echo "==> Deploy concluído: $NEW_TAG está no ar."
    echo "    Para voltar à versão anterior: sudo $0 $OLD_TAG"
    exit 0
  fi
  sleep 2
done

echo "ERRO: o site não respondeu em 60 segundos." >&2
echo "Para voltar à versão anterior: sudo $0 $OLD_TAG" >&2
exit 1