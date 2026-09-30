# 0012. Hospedagem na Oracle Always Free, numa VM A1 descartável, com Docker Compose

- **Status:** Aceito (o shape da VM foi alterado provisoriamente pelo ADR 0020)
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D20, D21, D33; §10.7

## Contexto

O requisito é custo zero sem prazo de validade, com portabilidade. O autor já opera uma VM Oracle Always Free (a agenda: Nginx + Certbot + systemd + DuckDNS). A documentação da Oracle diz que instâncias Always Free **podem ser recuperadas** se, durante 7 dias, CPU (p95), rede e memória (só A1) ficarem **todas** abaixo de 20%. Uma loja de portfólio passa a maior parte do tempo parada.

## Decisão

- **Oracle Cloud Always Free, VM Ampere A1 (ARM64)**, separada da VM da agenda, dimensionada no necessário (ex.: 1 OCPU / 6 GB).
- **A aplicação e o PostgreSQL em Docker Compose**, com imagens ARM64. O PostgreSQL **não publica porta**; a aplicação escuta só em `127.0.0.1`.
- **Nginx + Certbot no host**, como na agenda, com um novo subdomínio **DuckDNS**.
- **A VM é descartável:** um script versionado em `deploy/` a reconstrói do zero, e o banco volta do backup fora do provedor (ADR 0018). O script é executado de verdade pelo menos uma vez no M0.

## Alternativas consideradas

- **Azure for Students:** o crédito acaba (por ano ou na formatura), e exigiria uma migração depois.
- **Segunda VM E2.1.Micro:** 1 GB para .NET mais PostgreSQL fica apertado demais.
- **Mesma VM da agenda:** mistura falhas, recursos e segurança de dois sistemas.
- **Converter a conta para Pay As You Go** (para tentar evitar a recuperação): exige cartão e elimina a garantia estrutural de "sem cartão, sem cobrança". A isenção não foi confirmada na documentação lida.
- **Gerar carga artificial para parecer ativo:** fere o espírito do plano gratuito e pode violar os termos.
- **systemd sem Docker (como a agenda):** não resolve o PostgreSQL, o ARM e a portabilidade com a mesma facilidade.
- **Caddy no Compose:** novidade sem ganho; o caminho Nginx + Certbot já é dominado.

## Consequências

- **Positivas:** custo zero indefinido; mudar de provedor = instalar o Docker, subir o Compose e restaurar o backup.
- **Negativas:** o autor é o operador (atualizações, hardening); a loja pode ficar fora do ar até a reconstrução, se a VM for recuperada; a capacidade A1 às vezes está indisponível em algumas regiões.
- **Armadilha registrada:** portas publicadas pelo Docker passam por cima do UFW. Nunca publicar em `0.0.0.0`.
- **Como saber se deu errado:** o monitor externo alertando indisponibilidade; o script de reconstrução falhando no teste.
