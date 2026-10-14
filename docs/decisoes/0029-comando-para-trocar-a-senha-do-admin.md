# 0029. Comando `reset-admin-password` para trocar a senha de um admin

- **Status:** Aceito
- **Data:** 2026-10-15
- **Relacionado:** ADR 0013, ADR 0023 (complementa a consequência "trocar a senha do admin hoje é apagar a conta"), `docs/DEFINICOES.md` D24

## Contexto

O ADR 0023 deixou uma lacuna registrada nas consequências: **não existe jeito de trocar a senha de um admin**. O `create-admin` não mexe na senha de uma conta que já existe, e a única saída era apagar a conta por SQL e criá-la de novo.

Em 15/10/2026, ao testar o M5 em produção, o autor não tinha mais a senha do admin, que tinha sido gerada e mostrada uma vez só em 06/10. Além disso, essa senha **apareceu no chat em 06/10**, e trocá-la já estava no checklist do M6.

## Decisão

Um comando novo, irmão do `create-admin`:

```
dotnet KeycapStore.Web.dll reset-admin-password --email <e-mail>
```

1. **Só vale para quem já é `Admin`.** Para conta de cliente ou e-mail inexistente, o comando recusa e não muda nada. Ele não é um "trocar senha de qualquer um".
2. A senha nova é gerada como no `create-admin` (20 caracteres, `RandomNumberGenerator`) e **mostrada uma vez** no terminal. Não vai para log nem para nenhum arquivo.
3. A troca usa o próprio Identity (`RemovePasswordAsync` + `AddPasswordAsync`) **numa transação**: ou troca tudo, ou nada, e a conta nunca fica sem senha.
4. O carimbo de segurança da conta (`SecurityStamp`) muda, e o Identity derruba as sessões abertas com a senha antiga na próxima checagem do cookie (até 30 minutos, o padrão do ASP.NET).
5. Como o `create-admin`, ele só roda por quem já está **dentro da VM** (SSH). Nenhuma rota HTTP troca senha de admin.

## Alternativas consideradas

- **Criar outra conta de admin com outro e-mail:** funciona sem código, mas deixa a conta antiga, com a senha vazada, ativa até alguém lembrar de remover. E o problema volta na próxima vez que uma senha for esquecida.
- **Escrever o hash da senha direto no banco (SQL):** frágil. O formato do hash é interno do Identity, e um erro deixa a conta inacessível.
- **Tela "esqueci minha senha" com e-mail:** precisa de envio de e-mail, um serviço novo fora do escopo da v1.

## Consequências

- **Positivas:** a senha vazada de 06/10 pode ser trocada já (item do M6 resolvido); esquecer a senha deixa de ser um problema; testes cobrem admin, cliente e e-mail inexistente.
- **Negativas / custos:** mais um comando no `Program.cs` para manter.
- **Como saber se deu errado:** qualquer caminho que troque a senha de um **cliente** por este comando, ou uma senha nova aparecendo em log.
