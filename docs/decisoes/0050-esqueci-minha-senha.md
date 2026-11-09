# 0050. "Esqueci minha senha": link de 30 minutos pelo e-mail, mesma resposta para todos, admin fora

- **Status:** Aceito
- **Data:** 2026-11-09
- **Relacionado:** ADR 0049 (e-mails da loja), ADR 0029 (troca de senha do admin pelo comando), ADR 0039 (login com Google), ADR 0044 (sessão reconferida a cada requisição), ADR 0015 (dados pessoais)

## Contexto

Segundo item da V4. Hoje quem esquece a senha perde a conta. Com o e-mail da loja pronto (ADR 0049), a troca pelo e-mail fica possível.

Conferido na documentação oficial (09/11):

- **Microsoft (Identity):** o link de troca usa `GeneratePasswordResetTokenAsync` e a troca usa `ResetPasswordAsync`. Esses tokens valem **1 dia** por padrão, mudável por `DataProtectionTokenProviderOptions.TokenLifespan`, que vale para todos os tokens desse tipo.
- **OWASP (Forgot Password Cheat Sheet):**
  - a mesma mensagem exista a conta ou não, e no mesmo tempo;
  - limite de pedidos por conta;
  - token aleatório, que vence e **só vale uma vez**;
  - nenhuma mudança na conta antes de o token ser usado;
  - a senha nova digitada duas vezes, com a mesma política do cadastro;
  - **não** entrar automaticamente depois da troca;
  - encerrar as outras sessões;
  - **avisar por e-mail** que a senha foi trocada, sem a senha.

## Decisão

1. **Pedir a troca** (`/Account/ForgotPassword`, link "Esqueci minha senha" na página de entrar): o cliente digita o e-mail, e a página **sempre** mostra a mesma mensagem ("se existir uma conta com esse e-mail, enviamos um link..."). Ela nunca diz se a conta existe.
2. **O e-mail sai pela caixa de saída** (ADR 0049), nunca na requisição. A tabela passa a aceitar e-mails de **conta**:
   - ganha as colunas do cliente (`CustomerId`) e do idioma da página no momento do pedido (`Language`);
   - o pedido (`OrderId`) vira opcional.
3. **O link secreto não fica guardado.** Ele é gerado **na hora do envio**, com a situação da conta naquele momento:
   - **conta com senha** → link para `/Account/ResetPassword`, que vale **30 minutos** (decisão do autor; a validade de todos os tokens do Identity da loja passa a ser essa);
   - **conta só do Google** → e-mail explicando que ela entra pelo botão do Google (decisão do autor);
   - **admin** → e-mail dizendo que a senha de administrador só é trocada pelo comando `reset-admin-password` (decisão do autor; ADR 0029). Ninguém que tome a caixa de e-mail de um admin vira admin da loja;
   - **conta apagada ou sem e-mail** → descartado.
4. **Limite:** no máximo **1 e-mail a cada 5 minutos** por conta (decisão do autor). Pedidos repetidos nesse intervalo mostram a mesma mensagem e não anotam nada.
   - A conferência é "olha e depois grava": dois pedidos no mesmo instante podem mandar dois e-mails. Não é uma garantia de pagamento, então não vale um índice.
   - O limite por endereço de rede fica para o item "limite de tentativas" da V4.
5. **Trocar a senha** (`/Account/ResetPassword`): senha nova duas vezes, com a mesma regra do cadastro. Tudo numa transação:
   - a troca muda o *security stamp*, então o link **para de valer** e as outras sessões caem (ADR 0044);
   - o bloqueio por senha errada é zerado;
   - o e-mail **"Sua senha foi trocada"** é anotado.

   Depois, a página de entrar mostra "Senha trocada. Entre com a senha nova." **Não** entra sozinho.
6. **Link inválido, vencido ou já usado,** e também conta de admin ou sem senha: "Este link é inválido ou venceu. Peça outro." A página não diz qual foi o motivo.
7. **Logs** só com o id do cliente, nunca o e-mail nem o token (ADR 0015).

## Alternativas consideradas

- **Link de 1 dia** (padrão do Identity) ou de 1 hora: o autor preferiu 30 minutos. Um e-mail que caia no spam pode vencer antes de ser achado; aí basta pedir outro.
- **Guardar o link na caixa de saída:** quem lesse o banco ou um backup conseguiria trocar a senha. Gerar no envio evita isso.
- **Mandar o e-mail na própria requisição:** mais rápido, mas o tempo de resposta mudaria conforme a conta existe ou não (a OWASP pede tempo igual) e uma queda do Gmail viraria erro na página.
- **Deixar a conta do Google criar uma senha pelo link:** muda a regra do ADR 0039; ficou fora.
- **Admin igual ao cliente:** mais prático, mas a segurança do admin passaria a depender da caixa de e-mail.

## Consequências

- **Positivas:**
  - quem esquece a senha recupera a conta sozinho;
  - a página não revela quais e-mails têm conta;
  - um link vazado vale no máximo 30 minutos e uma única vez;
  - o aviso de troca ajuda a perceber uma troca que a pessoa não pediu.
- **Negativas / custos:**
  - duas páginas novas com tradução;
  - colunas novas na caixa de saída (migration);
  - o e-mail pode levar até 1 minuto (o job), e o limite de 5 minutos atrapalha quem pede de novo logo em seguida;
  - o tempo de resposta não é idêntico (um `SELECT` e talvez um `INSERT` a mais), mas o envio não acontece na requisição.
- **Como saber se deu errado:**
  - um link usado duas vezes;
  - uma sessão antiga que continua aberta depois da troca;
  - uma página que se comporta diferente para um e-mail sem conta;
  - um admin trocando a senha pelo e-mail;
  - um token aparecendo em log ou no banco.
