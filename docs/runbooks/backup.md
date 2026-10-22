# Runbook: backup e restauração do banco

Decisões: ADR 0018 (observabilidade e backups) e ADR 0030 (Cloudflare R2 como destino).

## Como funciona

- Todo dia, às **02:30** (horário de Brasília, com até 10 min de atraso aleatório), o timer `keycapstore-backup.timer` roda o `/opt/keycapstore/backup.sh`.
- O script:
  1. faz o `pg_dump` (formato `custom`, já compactado);
  2. criptografa com a **chave pública** do `age` (`/opt/keycapstore/backup-recipient.txt`);
  3. guarda em `/var/backups/keycapstore/`, mantendo **só as 2 cópias mais recentes**;
  4. envia para a R2: `daily/` (todo dia) e `weekly/` (aos domingos, em UTC);
  5. avisa o healthchecks.io: `HEALTHCHECK_URL` no sucesso, `HEALTHCHECK_URL/fail` na falha.
- Na R2:
  - as regras de ciclo de vida apagam `daily/` depois de 8 dias e `weekly/` depois de 29 dias;
  - o bucket lock impede apagar ou sobrescrever `daily/` por 7 dias e `weekly/` por 28 dias.
- A **chave privada** do `age` fica **só com o autor**, na pasta `OneDrive\Documentos\Backups Keycapstore`. Ela **nunca** vai para a VM. Sem ela, nenhum backup pode ser aberto.

## Arquivos na VM

| Arquivo | Dono / permissão | Conteúdo |
|---|---|---|
| `/opt/keycapstore/backup.sh` | root, 750 | o script (vem do repositório, `deploy/backup.sh`) |
| `/opt/keycapstore/backup-recipient.txt` | root | a chave **pública** do `age` (`age1...`) |
| `/opt/keycapstore/backup.env` | root, **600** | credenciais da R2 e URL do healthchecks.io (modelo em `deploy/backup.env.example`) |
| `/etc/systemd/system/keycapstore-backup.{service,timer}` | root | o agendamento (vem do repositório) |

## Conferir se está funcionando

```bash
systemctl list-timers keycapstore-backup.timer
sudo journalctl -u keycapstore-backup --since today
sudo ls -l /var/backups/keycapstore/
```

E o painel do healthchecks.io: o check `keycapstore-backup` deve estar verde.

## Rodar um backup agora

```bash
sudo systemctl start keycapstore-backup.service
sudo journalctl -u keycapstore-backup -n 20
```

## Restaurar (teste mensal ou de verdade)

O teste mensal restaura num **banco separado do PC**, nunca por cima de produção.

### 1. Pegar o arquivo

O mais recente pode vir **da VM** ou **da R2**.

- **Da VM.** O arquivo é do root, então primeiro copie para a sua pasta (o `sh -c` faz o root expandir o `*`):

  ```bash
  sudo sh -c 'cp /var/backups/keycapstore/keycapstore-*.dump.age /home/ubuntu/ && chown ubuntu /home/ubuntu/keycapstore-*.dump.age'
  ```

  No Developer PowerShell, **sem `\` antes da aspa final**:

  ```powershell
  scp 'keycapstore:keycapstore-*.dump.age' "$env:USERPROFILE\OneDrive\Documentos\Backups Keycapstore"
  ```

  Depois, apague a cópia na VM: `rm ~/keycapstore-*.dump.age`.

- **Da R2** (se a VM não existir mais). No painel da Cloudflare, abra **R2 → bucket `keycapstore-backups` → `daily/`** e baixe o arquivo mais recente para a pasta `Backups Keycapstore`.

### 2. Restaurar no banco do PC (Developer PowerShell, na pasta do projeto)

Troque `NOME_DO_ARQUIVO` pelo nome real:

```powershell
docker compose up -d db
docker compose cp "$env:USERPROFILE\OneDrive\Documentos\Backups Keycapstore\NOME_DO_ARQUIVO" db:/tmp/backup.age
docker compose cp "$env:USERPROFILE\OneDrive\Documentos\Backups Keycapstore\keycapstore-backup.key" db:/tmp/key.txt
docker compose exec db sh -c "apk add -q age && age -d -i /tmp/key.txt -o /tmp/restore.dump /tmp/backup.age && rm /tmp/key.txt"
docker compose exec db psql -U keycapstore -d postgres -c "CREATE DATABASE restore_test"
docker compose exec db pg_restore -U keycapstore -d restore_test --no-owner /tmp/restore.dump
```

### 3. Conferir

Entre no `psql` e digite a consulta **lá dentro** (passar o SQL pelo `-c` no PowerShell 5.1 estraga as aspas):

```powershell
docker compose exec db psql -U keycapstore -d restore_test
```

```sql
SELECT "Status", count(*) FROM orders GROUP BY "Status" ORDER BY "Status";
```

Em produção, conte **só os pedidos criados antes do backup** (a hora está no nome do arquivo, em UTC; `20261022T053150Z` vira `2026-10-22T05:31:50Z`):

```bash
cd /opt/keycapstore && sudo docker compose exec db psql -U keycapstore -d keycapstore
```

```sql
SELECT "Status", count(*) FROM orders WHERE "CreatedAt" < 'HORA_DO_BACKUP' GROUP BY "Status" ORDER BY "Status";
```

As duas listas devem ser iguais. (Um pedido antigo que mudou de situação depois do backup também gera diferença; se aparecer, confira se é o caso antes de suspeitar do backup.)

### 4. Limpar

O dump aberto tem dados de clientes:

```powershell
docker compose exec db rm /tmp/restore.dump /tmp/backup.age
docker compose exec db psql -U keycapstore -d postgres -c "DROP DATABASE restore_test"
```

### Restaurar em produção (desastre)

1. Reconstruir a VM com `deploy/bootstrap-vm.sh`.
2. Subir só o banco e restaurar nele (`pg_restore --clean --if-exists` no banco `keycapstore`).
3. Fazer o deploy da versão atual.
4. Conferir os pedidos pagos depois do último backup no Dashboard da Stripe (o `order_id` está nos metadados de cada sessão).

## Histórico de testes de restauração

| Data | Backup | Resultado |
|---|---|---|
| 15/10/2026 | `keycapstore-20261015T145913Z.dump.age` (da VM) | ✅ contagem por status igual à de produção (2 Delivered, 2 Expired, 1 Failed, 1 Paid, 1 Picking, 2 Shipped) |
| 22/10/2026 | `keycapstore-20261022T053150Z.dump.age` (**da R2**, `daily/`, sem usar a VM) | ✅ contagem por status igual à de produção até a hora do backup (2 Delivered, 2 Expired, 2 Failed, 2 Paid, 1 Picking, 2 Refunded, 1 Returned, 2 Shipped). Próximo teste até 22/11/2026 |
