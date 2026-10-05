# 0018. Observabilidade externa, backups fora do provedor e atualizações automáticas

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D30–D32, §10.5–§10.6

## Contexto

Um sistema não consegue reportar a própria falha: se a VM cair, o disco encher ou a Oracle recuperar a instância, uma aplicação que "manda e-mail quando dá erro" fica em silêncio. E um e-mail por erro gera fadiga de alertas. Um backup guardado no mesmo disco que ele protege some junto com esse disco. Um backup nunca restaurado é só uma esperança.

## Decisão

**Observabilidade**
- **Serilog** em JSON na saída padrão, com **rotação de logs no Docker** (`max-size`, `max-file`). Cada linha do fluxo de pagamento tem o ID do evento e o ID do pedido, sem dados pessoais.
- **`/health`** verifica o banco e o espaço em disco (falha acima de cerca de 85%).
- **Monitor externo gratuito** consulta o `/health` e avisa quando o status **muda**.
- **Alertas de falha de webhook da própria Stripe**, independentes do servidor.

**Backups**
- `pg_dump` diário via timer do systemd, **criptografado com `age`** (só a chave pública fica na VM).
- Enviado para **armazenamento de outro provedor** (Cloudflare R2 ou Backblaze B2), com credencial **só de escrita**; a retenção (7 diários + 4 semanais) é feita pelo **ciclo de vida do bucket**.
- **Dead man's switch** (healthchecks.io): o silêncio gera um alerta.
- **Teste de restauração mensal** em runbook; o primeiro acontece antes do go-live.
- Cópias locais na VM só como restauração rápida complementar.

**Atualizações**
- `unattended-upgrades` para as atualizações de segurança do sistema, com reinício na madrugada quando necessário. **Ampliado pelo ADR 0021:** também as atualizações comuns e as do Docker, todo dia às 03:30.
- Dependabot para as imagens Docker, pacotes NuGet e actions (tudo via PR e CI).

## Alternativas consideradas

- **Aplicação enviando e-mail em cada erro:** falha exatamente quando a aplicação cai, gera fadiga de alertas e acrescenta mais um segredo (credenciais SMTP).
- **Backup só na VM, com 2 cópias:** não sobrevive à perda do disco nem à recuperação da instância, e um bug silencioso destrói as cópias boas em 2 dias.
- **Backup no Object Storage da própria Oracle:** não sobrevive a um problema na conta da Oracle.

## Consequências

- **Positivas:** um disco cheio é detectado antes de travar o banco; a perda máxima de dados é de 1 dia (e os pedidos pagos podem ser reconstruídos pelos metadados da Stripe).
- **Negativas:** mais peças para configurar no M6; a chave privada do `age` precisa ser guardada com cuidado pelo autor (sem ela, os backups são ilegíveis).
- **Pendente (Q4):** confirmar as condições atuais dos planos gratuitos do R2/B2, do monitor externo e do healthchecks.io.
- **Como saber se deu errado:** o dead man's switch disparando; o teste de restauração falhando.
