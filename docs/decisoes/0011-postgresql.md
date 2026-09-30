# 0011. PostgreSQL como banco de dados

- **Status:** Aceito (substitui a escolha inicial de SQL Server na Azure)
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D19

## Contexto

O autor domina SQL Server, e a primeira escolha foi SQL Server na Azure. Depois veio um requisito novo: custo zero **indefinido** e **portabilidade** para outro provedor gratuito, como a Oracle Always Free (onde a agenda roda). Lá, a VM E2.1.Micro tem 1 GB de RAM (o SQL Server no Linux exige no mínimo 2 GB), e as VMs Ampere A1 são ARM, onde o SQL Server não roda.

## Decisão

Usar **PostgreSQL** desde o início, via EF Core (Npgsql), rodando em container.

## Alternativas consideradas

- **SQL Server na Azure, com migração futura:** trabalho em dobro (código portável sem usar os recursos do SQL Server, mais a migração de dados depois).
- **SQL Server em qualquer VM gratuita:** não cabe na E2.1.Micro e não roda em ARM.

## Consequências

- **Positivas:** um banco só, que roda em qualquer lugar (x86, ARM, Docker local, qualquer provedor); PostgreSQL somado ao SQL Server amplia o currículo.
- **Negativas:** um banco novo para o autor (atenuado pelo EF Core); a experiência com SQL Server não aparece neste projeto.
- **Como saber se deu errado:** se aparecer necessidade de um recurso específico de outro banco.
