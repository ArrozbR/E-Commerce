# 0014. Gestão de segredos

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D25; `.claude/rules/pagamentos.md` §7

## Contexto

A chave secreta da Stripe (`sk_...`) permite criar cobranças e reembolsos. Se o segredo do webhook (`whsec_...`) vazar, qualquer pessoa consegue forjar um "pagou". Um segredo commitado fica no histórico do git para sempre, e **revogar e trocar** é a única correção real.

## Decisão

- **Desenvolvimento:** `dotnet user-secrets` (fora da pasta do projeto, então é impossível commitar por acidente).
- **Produção:** `.env` fora do repositório (ex.: `/opt/keycapstore/.env`, `chmod 600`), carregado pelo `env_file` do Compose. **Nunca** na imagem Docker nem no git.
- **`ValidateOnStart`:** a aplicação não sobe sem os segredos obrigatórios.
- **gitleaks no CI**, bloqueando o merge.
- Chaves de teste e de produção **nunca** no mesmo ambiente.

## Alternativas consideradas

- **`.env` na pasta do projeto, com `.gitignore`:** um erro no `.gitignore` basta para vazar.
- **Cofre gerenciado (Azure Key Vault, OCI Vault):** padrão em empresas grandes, mas prende a um provedor (contra a portabilidade) e acrescenta configuração.

## Consequências

- **Positivas:** várias barreiras independentes; o erro fica visível no CI antes do merge.
- **Negativas:** o `.env` de produção é gerenciado à mão na VM (e precisa ser recriado quando a VM é reconstruída; ele fica fora do backup do banco e é guardado pelo autor).
- **Como saber se deu errado:** o gitleaks acusando; um segredo aparecendo em log.
