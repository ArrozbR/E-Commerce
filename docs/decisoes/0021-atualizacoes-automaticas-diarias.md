# 0021. Atualizações automáticas diárias, incluindo as comuns e as do Docker

- **Status:** Aceito
- **Data:** 2026-10-05
- **Relacionado:** ADR 0018 (seção "Atualizações"), ADR 0012, ADR 0020

## Contexto

O ADR 0018 previa o `unattended-upgrades` só para as atualizações **de segurança**, que é o padrão do Ubuntu. Em 05/10/2026, a VM tinha 11 atualizações pendentes, incluindo um kernel novo (`7.0.0-1013-oracle`, publicado em `noble-updates`), que o robô ignorava de propósito. O reinício automático das 04:00 nunca tinha acontecido porque nenhuma atualização **de segurança** exigiu reinício desde a criação da VM. Além disso, os horários padrão do Ubuntu são sorteados (até 12h de atraso), e as atualizações podiam rodar no meio do dia.

O autor quer a VM sempre em dia, sem depender de lembrar de atualizar à mão.

## Decisão

**A VM instala todas as atualizações todo dia de madrugada, e reinicia só quando alguma exigir.**

- Origens aceitas pelo `unattended-upgrades`: segurança (padrão) **+ `${distro_codename}-updates` + `Docker:${distro_codename}`**.
- Horários fixos, sem sorteio (`RandomizedDelaySec=0`): **03:00** baixa a lista (`apt-daily.timer`), **03:30** instala (`apt-daily-upgrade.timer`). Se houver `/var/run/reboot-required`, a VM reinicia às **04:00**.
- O bootstrap usa `apt-get upgrade --with-new-pkgs`, para que kernels novos (pacotes novos) não fiquem para trás.
- Tudo vive no `deploy/bootstrap-vm.sh` (parte 2), e não só na VM.
- **Troca de versão do sistema (`do-release-upgrade`, ex.: Ubuntu 24 → 26) continua manual**, com plano próprio e, de preferência, recriando a VM pelo bootstrap.

## Alternativas consideradas

- **Só segurança (padrão, ADR 0018):** rejeitada, porque as atualizações comuns (inclusive kernels) se acumulam sem que ninguém perceba.
- **Atualizar à mão de vez em quando:** depende de memória humana; já tinha falhado (11 pendentes em 5 dias).
- **Reiniciar todo dia às 04:00:** queda diária de 1–2 minutos sem ganho quando nada mudou no kernel.

## Consequências

- **Positivas:** servidor sempre em dia; kernels novos entram no ar sozinhos; horário previsível, de madrugada.
- **Negativas / custos:** uma atualização comum pode, raramente, mudar um comportamento sem ninguém olhar. Uma atualização do Docker reinicia os containers (cobertos pelo `restart: unless-stopped`). Mitigação: rollback pelo `deploy.sh` e a VM recriável pelo bootstrap.
- **Como saber se deu errado:** a loja fora do ar de manhã depois de uma atualização (o monitor externo do M6 vai avisar); erros em `/var/log/unattended-upgrades/`.
