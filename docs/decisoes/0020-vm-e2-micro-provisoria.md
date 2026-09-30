# 0020. VM E2.1.Micro como hospedagem provisória, até haver capacidade A1

- **Status:** Aceito (provisório)
- **Data:** 2026-09-30
- **Relacionado:** ADR 0012 (altera só o shape da VM); `docs/DEFINICOES.md` D20

## Contexto

O ADR 0012 escolheu uma VM **Ampere A1** (ARM, 1 OCPU / 6 GB) na Oracle Always Free. Na primeira tentativa de criação (30/09/2026), a Oracle respondeu:

> "Out of capacity for shape VM.Standard.A1.Flex in availability domain AD-1."

A região de São Paulo (a *home region* da conta) tem um único availability domain, então não havia outro onde tentar. Sem VM, o M0 (deploy, HTTPS, página no ar) ficaria bloqueado por tempo indeterminado. A conta ainda tinha uma vaga de **E2.1.Micro** (limite "1 of 2", a outra é da agenda).

## Decisão

Criar a VM `keycapstore` como **VM.Standard.E2.1.Micro** (x86_64, 1/8 OCPU com rajadas, **1 GB** de RAM, Ubuntu 24.04), na mesma VCN e sub-rede pública da agenda, **como solução provisória**. Continuar tentando a A1 de tempos em tempos. Quando houver capacidade, migrar rodando o script de reconstrução numa VM A1 nova e restaurando o backup.

Consequências obrigatórias para caber em 1 GB:
- **Swap** (≈2 GB) criado pelo script de reconstrução, antes de tudo.
- **Nada é compilado na VM.** A imagem vem pronta do CI.
- PostgreSQL e .NET configurados para usar pouca memória, quando o uso real mostrar necessidade.

## Alternativas consideradas

- **Esperar a A1:** bloquearia o M0 sem prazo.
- **A2.Flex:** aparece na lista, mas **não é Always Free**. Violaria o custo zero.
- **Conta Pay As You Go (para ter prioridade de capacidade):** exige cartão e remove a garantia de "sem cartão, sem cobrança" (já rejeitada no ADR 0012).
- **Mesma VM da agenda:** mistura falhas, recursos e segurança de dois sistemas (já rejeitada no ADR 0012).

## Consequências

- **Positivas:** destrava o M0 hoje. Sendo x86_64, a imagem Docker tem a mesma arquitetura do PC de desenvolvimento e do CI, então **não é preciso build ARM64** enquanto durar a Micro. A migração futura é barata, porque a VM é descartável e montada por script.
- **Negativas:** 1 GB é apertado (o Ubuntu sozinho usa ~350 MB, e sobram ~600 MB). Com swap, o sistema fica mais lento sob carga. O CPU de 1/8 de núcleo deixa a primeira requisição e as migrations lentas.
- **Como saber se deu errado:** processos mortos por falta de memória (OOM killer), swap constantemente cheio, ou `/health` falhando por timeout. Nesse caso, priorizar a migração para a A1 ou reavaliar a hospedagem com um novo ADR.
