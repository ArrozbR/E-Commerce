# 0001. Projeto de portfólio em modo teste, pronto para produção, com custo zero

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D01, D02, D33

## Contexto

O objetivo é um projeto de portfólio para vagas .NET em empresas médias e grandes, sem data de entrega. A ideia inicial era "pagamento real". Receber dinheiro de verdade exige uma conta Stripe ativada, algo real para vender e entregar, e obrigações legais e fiscais (CDC, nota fiscal, LGPD, chargebacks). O autor tem plano de estudante e não quer gastar nada.

## Decisão

- A loja opera em **modo teste da Stripe**, com o código **pronto para produção**: ir para produção é só trocar chaves e configuração, sem mudar o código.
- **Custo zero** é requisito: todo serviço e recurso deve estar num plano gratuito sem prazo de validade.
- O projeto evolui em **incrementos pequenos e independentes**, sem prazo, mas com marcos e estimativas (ver `docs/STATUS.md`).

## Alternativas consideradas

- **Produção com dinheiro real:** exige virar comerciante (obrigações fiscais, de consumidor e de suporte), e **um recrutador não conseguiria testar a loja sem gastar dinheiro**.
- **Produção só numa versão futura:** mantida como possibilidade; o checklist de produção fica em `docs/DEFINICOES.md` §11.

## Consequências

- **Positivas:** qualquer pessoa testa a loja ao vivo com o cartão `4242...`; nenhum risco legal ou financeiro.
- **Negativas:** "pronto para produção" precisa ser **verificável**, e não só afirmado. Por isso existe o checklist em §11.
- **Como saber se deu errado:** se aparecer algum `if (teste)` no fluxo de pagamento, ou se ir para produção exigir mudar código.
