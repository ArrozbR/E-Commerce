# 0049. V4 e e-mails da loja: Gmail próprio da loja, caixa de saída no banco e job de envio

- **Status:** Aceito
- **Data:** 2026-11-09
- **Relacionado:** ADR 0025 (roadmap), ADR 0031 (cancelamento), ADR 0032 (arrependimento), ADR 0037 (conciliação), ADR 0042 (pré-venda), ADR 0014 (segredos), ADR 0015 (dados pessoais); `docs/DEFINICOES.md` §3 e D30; `.claude/rules/pagamentos.md` §2, item 6

## Contexto

A V3 (interface) foi concluída em 06/11. Na mesma sessão, o autor dividiu o que faltava: o que é só tela fechou a V3, e o resto virou a **V4**, que fala de contas, e-mails e operação:

1. serviço de e-mail e os e-mails da loja;
2. "esqueci minha senha";
3. "avise-me quando voltar";
4. na "Minha conta": ligar e desligar os e-mails de novidades (consentimento) e trocar o e-mail;
5. reforços da auditoria (CSP e limite de tentativas no login e no cadastro);
6. painel do admin (vendas do dia, mais vendidos, estoque baixo) no visual novo;
7. reposição automática de estoque (a decidir num ADR próprio);
8. cupom de desconto (por último, com um ADR próprio).

Os itens 2, 3 e 4 dependem do e-mail, que vem primeiro. O e-mail também resolve pendências do go-live: o Decreto 7.962 pede que a loja **envie** a confirmação da desistência (ADR 0032), e hoje o cliente não recebe aviso de pagamento, envio, reembolso nem do resultado do lote da pré-venda.

Conferido na documentação oficial (09/11):

- **Resend** (100 e-mails por dia, 3.000 por mês no plano grátis) e **Brevo** (300 por dia) exigem um **domínio próprio autenticado**. O Resend pede um MX e um TXT em `send` e um TXT em `resend._domainkey`. O Brevo diz que domínios de e-mail grátis (Gmail, Yahoo) não podem ser autenticados. O **Mailgun** grátis dá 100 por dia, um domínio e guarda os registros de envio por só 1 dia.
- **DuckDNS:** um único registro TXT por domínio, que "vale para todos os subdomínios", e nenhum MX configurável. Não cabem os registros diferentes de SPF e DKIM, então o `keycapstore.duckdns.org` não serve para mandar e-mail por um serviço.
- **deSEC** (`dedyn.io`, DNS grátis com TXT, MX e CNAME): os cadastros novos estão **suspensos desde 21/09/2026**, sem data para voltar (fórum oficial, 06/10).
- **Gmail:** servidor `smtp.gmail.com`, porta 587 com TLS e login obrigatórios; limite de 500 e-mails por dia. Fora do navegador, o login é feito com uma **senha de app**, que exige a verificação em duas etapas. O Google escreve que senhas de app "não são recomendadas" e servem para apps que não entram com o Google.
- **.NET:** a Microsoft não recomenda o `SmtpClient` para código novo ("não suporta muitos protocolos modernos") e indica o **MailKit** (licença MIT, versão estável 4.18.2, compatível com `net10.0`).
- **Oracle Cloud:** contas criadas depois de 23/06/2021 têm a **porta 25** bloqueada para fora. A documentação não diz nada sobre a 587 para servidores de terceiros. **Testado pelo autor na VM (09/11): a conexão com `smtp.gmail.com:587` abre.**
- **Mailpit** (`axllent/mailpit`, licença MIT): servidor de e-mail falso para desenvolvimento, com SMTP na porta 1025 e uma página na 8025 que mostra os e-mails recebidos.

## Decisão

1. **A V4 segue a lista do contexto, nessa ordem**, começando pelos e-mails. A reposição automática e o cupom ganham ADRs próprios quando chegarem.
2. **O envio é pelo Gmail** (decisão do autor, 09/11), com uma **conta Gmail só da loja**, criada pelo autor e sem uso pessoal, com verificação em duas etapas e uma senha de app. A conexão é `smtp.gmail.com:587` com STARTTLS, pelo **MailKit**. O remetente é "KeycapStore" com o endereço dessa conta. A peça que entrega o e-mail fica atrás de uma porta na `Application` (`IEmailSender`): se um domínio próprio ficar disponível (deSEC de volta, `is-a.dev`, domínio comprado), trocar para um serviço como o Resend muda só essa peça e a configuração.
3. **Caixa de saída no banco** (padrão *outbox*). Nenhuma requisição manda e-mail, e o webhook continua rápido (`pagamentos.md` §2, item 6).
   - Quem muda o pedido **anota** o e-mail numa tabela, **na mesma transação** da mudança.
   - Se a transação for desfeita, o e-mail some junto. Se a transição não acontecer (evento repetido, pedido já pago), nada é anotado.
   - Cada linha guarda só o **tipo** do e-mail, o **id do pedido**, a situação (`Pending`, `Sent`, `Failed` ou `Discarded`), o número de tentativas, a próxima tentativa e as datas.
   - A chave (tipo + pedido) é **única**, como defesa contra uma segunda anotação.
   - A linha **não guarda o destinatário nem o texto**. O e-mail é montado na hora do envio, com o e-mail atual da conta e o idioma do pedido (`Order.Language`, ADR 0038).
   - Pedido sem dono (conta excluída, ADR 0033) → o e-mail vira `Discarded`, sem envio.
   - O código fica numa pasta de módulo nova, **`Notifications`**, na `Application` (a porta `IEmailSender` e os tipos de e-mail) e na `Infrastructure` (a tabela, o job e o envio).
4. **Os quatro primeiros e-mails** (escolhidos pelo autor):
   - **Pagamento confirmado:** quando o pedido vira `Paid`, pelo webhook ou pela conciliação, que usam o mesmo código (`CheckoutSessionTransitions`). Na pré-venda, o e-mail traz o envio previsto.
   - **Pedido enviado:** quando o admin marca `Shipped`, com o código de rastreio.
   - **Reembolso feito:** quando o pedido vira `Refunded` (`RefundProcessor`, depois de a Stripe aceitar o reembolso). O texto diz que o pedido foi cancelado e o valor devolvido; quando foi o lote da pré-venda que não atingiu o mínimo, ele diz isso. O pedido não guarda se foi o cliente ou a falta de estoque, então esses dois casos têm o mesmo texto (a página do pedido mostra a situação). O reembolso de uma desistência (`AwaitingReturn`) não gera e-mail novo: o da desistência já avisou.
   - **Desistência recebida:** quando o cliente desiste (`Withdraw`). É a confirmação exigida pelo Decreto 7.962, com o reembolso e o que acontece depois.
5. **O job envia.** É uma tarefa nova no `OrderMaintenanceJob` (a cada minuto, no seu próprio `try`). Ela pega até 20 e-mails `Pending` com a próxima tentativa vencida, do mais antigo ao mais novo.
   - **Deu certo** → `Sent`.
   - **Falha temporária** (Gmail fora, tempo esgotado, recusa temporária) → nova tentativa depois de **1 min, 5 min, 30 min, 2 h, 6 h e 12 h**. Depois da última (cerca de 21 h), vira `Failed` com um erro no log (decisão do autor).
   - **Recusa permanente do servidor** (endereço inválido, por exemplo) → `Failed` na hora.
   - O pedido nunca é afetado por um e-mail que falhou.
   - Os logs registram só o id do e-mail, o tipo e o id do pedido, **nunca o endereço** (ADR 0015).
6. **Corridas:** se duas execuções anotam o mesmo e-mail ao mesmo tempo (dois eventos de pagamento, duas rodadas de reembolso), o índice único recusa a segunda, e a transação inteira dela é desfeita, como já acontecia com o token de concorrência do pedido: o webhook responde 500 e a Stripe reenvia, e o job de reembolso ignora o pedido nessa rodada.
7. **Pelo menos uma vez, não exatamente uma vez.** Se o Gmail aceitar o e-mail e a gravação do `Sent` falhar logo depois, o e-mail sai de novo na rodada seguinte. Um e-mail repetido é aceitável; um e-mail perdido não.
8. **Formato:** texto simples e uma versão HTML sem imagens, sem rastreamento de abertura e sem links de terceiros, no idioma do pedido. O link para o pedido usa um endereço público em configuração (`Store:PublicUrl`, validado na subida), porque o job não tem uma requisição de onde tirar o endereço.
9. **Configuração e segredos** (ADR 0014):
   - O servidor, a porta e o nome do remetente ficam no `appsettings.json`.
   - O usuário (o endereço da conta) e a **senha de app** são segredos: `user-secrets` no PC e `.env` na VM (`EMAIL_USERNAME`, `EMAIL_PASSWORD`), exigidos pelo `compose.prod.yaml`, com `ValidateOnStart`.
   - A senha de app nunca aparece no chat, em log ou em print.
10. **Desenvolvimento e testes:**
   - No PC, o e-mail vai para o **Mailpit** (`compose.override.yaml`, publicado só em `127.0.0.1`); o PC nunca manda e-mail de verdade.
   - Nos testes, um `FakeEmailSender`. Os testes de integração conferem as linhas da caixa de saída e as tentativas (sucesso, falha temporária, recusa permanente, conta excluída, transição repetida sem segunda linha).
   - Nenhum teste fala com o Gmail.
11. **Limpeza:** linhas `Sent`, `Failed` e `Discarded` com mais de 30 dias são apagadas pelo mesmo job.

## Alternativas consideradas

- **Resend ou Brevo com o domínio do DuckDNS:** o DuckDNS não comporta os registros SPF e DKIM (um TXT só, sem MX).
- **Resend com um domínio grátis do deSEC (`dedyn.io`):** era a escolha do autor, mas os cadastros estão suspensos sem data. Fica como troca futura (decisão 2).
- **Comprar um domínio:** a melhor entrega, mas quebra a regra do custo zero.
- **`is-a.dev`** (subdomínio grátis por PR no GitHub): aceita MX e TXT pela documentação, mas as regras de uso não foram conferidas e a aprovação depende de voluntários. Fica como troca futura.
- **Nenhum serviço, só a caixa de saída numa página do admin:** ninguém receberia nada, e o "esqueci minha senha" não funcionaria.
- **`SmtpClient` do próprio .NET:** sem pacote novo, mas a Microsoft não o recomenda para código novo.
- **Enviar dentro da requisição** (webhook, botão do admin): proibido para o webhook (`pagamentos.md` §2, item 6). Prenderia a mudança do pedido à disponibilidade do Gmail.
- **Fila externa (Redis, RabbitMQ):** fora de escopo (`DEFINICOES.md` §3). A tabela no banco resolve com a mesma transação do pedido.
- **Guardar o destinatário e o texto prontos na linha:** mais simples de enviar, mas copiaria dado pessoal para outra tabela, que precisaria entrar na exclusão de conta e no "baixar meus dados".

## Consequências

- **Positivas:**
  - o cliente recebe os avisos do pedido e a confirmação da desistência exigida pela lei;
  - nenhuma falha de e-mail afeta pagamento ou pedido;
  - a base (caixa de saída + job + porta) serve para os próximos e-mails da V4;
  - trocar o Gmail por um serviço com domínio próprio muda só uma peça.
- **Negativas / custos:**
  - o remetente é um Gmail, menos profissional, com limite de 500 por dia;
  - a senha de app dá acesso à conta inteira (por isso uma conta só da loja), e o Google pode restringir contas que mandam e-mails automáticos (não confirmado);
  - o primeiro tráfego de saída por SMTP da VM (a porta 587 abriu no teste de 09/11, mas a Oracle pode mudar a regra);
  - um pacote novo (MailKit), uma imagem nova no PC (Mailpit), dois segredos novos e uma tabela nova;
  - textos novos nos dois idiomas;
  - e-mails repetidos são possíveis (decisão 7);
  - contas de teste com e-mail inventado (`pedro2@test.com`) geram e-mails devolvidos ao Gmail da loja.
- **Como saber se deu errado:**
  - linhas `Failed` se acumulando ou erros de envio no log;
  - e-mails da loja caindo no spam do Gmail;
  - o Google bloqueando a conta ou a senha de app;
  - a porta 587 fechada na VM;
  - um e-mail anotado para uma transição que não aconteceu.
