# 0023. Admin pelo comando também no PC (sem seed) e promoção de conta existente

- **Status:** Aceito
- **Data:** 2026-10-06
- **Relacionado:** ADR 0013 (substitui o item "seed de admin em `Development`"), `docs/DEFINICOES.md` D24

## Contexto

O ADR 0013 decidiu que o primeiro admin nasce pelo comando `create-admin` e que, em `Development`, um seed criaria um admin de teste ao ligar o site. Ao implementar o comando (M1), ficaram duas perguntas: o seed ainda vale a pena, e o que fazer quando o e-mail informado já tem conta.

## Decisão

1. **Não existe seed de admin.** No PC, o admin de teste é criado com o mesmo comando da produção:
   `dotnet run --project src/KeycapStore.Web -- create-admin --email <e-mail>`.
2. **Se o e-mail já tem conta, ela é promovida a `Admin`**, e a senha dela **não muda** (nenhuma senha é gerada nem mostrada).
3. Conta nova: senha de 20 caracteres gerada com `RandomNumberGenerator`, mostrada **uma vez** no terminal, nunca registrada em log nem guardada.
4. O comando é **idempotente**: rodar de novo não duplica conta nem papel.

## Alternativas consideradas

- **Seed automático em `Development`:** código que só roda no PC, escondido na inicialização, e mais uma senha para guardar (nos user-secrets). Um caminho só, o do comando, é mais simples e é exatamente o que roda em produção.
- **Recusar e-mail que já tem conta:** mais rígido, mas obrigaria a usar outro e-mail só para virar admin, sem ganho de segurança (quem roda o comando já está dentro da VM).

## Consequências

- **Positivas:** um único caminho para criar admin, coberto por testes de integração (conta nova, conta existente, rodar duas vezes); nada roda escondido ao ligar o site.
- **Negativas / custos:** depois de recriar o banco do PC, é preciso rodar o comando de novo. Não há tela para trocar senha: trocar a senha do admin hoje é apagar a conta (SQL) e rodar o comando de novo.
- **Como saber se deu errado:** mais de um admin inesperado em `AspNetUserRoles`; qualquer caminho HTTP que conceda o papel `Admin` (continua proibido pelo ADR 0013).
- **Recomendação de uso:** o admin de produção usa um e-mail separado da conta de cliente (ex.: `nome+admin@...`).
