# 0039. Login com Google: conta nova na hora, nunca junta sozinho com uma conta com senha, admin só com senha

- **Status:** Aceito
- **Data:** 2026-10-28
- **Relacionado:** ADR 0025 (roadmap da v2, plano gratuito verificado antes), ADR 0023 (admin pelo comando), ADR 0033 (exclusão de conta), ADR 0038 (site em inglês); `docs/DEFINICOES.md` §3 e §6

## Contexto

Item 5 da segunda parte da v2. Hoje a conta só existe com e-mail e senha (Identity, M1), e **o e-mail não é confirmado no cadastro**: qualquer pessoa pode criar uma conta com qualquer e-mail.

Conferido na documentação oficial (28/10):
- **Microsoft** ("Google external login setup in ASP.NET Core", .NET 10): pacote `Microsoft.AspNetCore.Authentication.Google`, `AddGoogle` com `ClientId` e `ClientSecret` (guardados em `user-secrets` com as chaves `Authentication:Google:ClientId`/`ClientSecret`; em variável de ambiente, `__` no lugar de `:`); o Google devolve a pessoa para `/signin-google`, que precisa estar cadastrado no console do Google (um endereço para o PC e um para produção).
- **Google** (verificação do app): apps que pedem só os dados básicos não precisam de verificação. (Público do app): o modo "Testing" limita a 100 usuários de teste e vence a autorização em 7 dias, **exceto** quando o pedido é só dos dados básicos (`email`, `profile`, `openid`); o aviso de "app não verificado" e o teto de 100 usuários novos valem para dados sensíveis.
- **Google** (OpenID Connect): o parâmetro `prompt` aceita só `none`, `consent` e `select_account`; não há como exigir que a pessoa digite a senha de novo (`max_age` não é aceito); o `auth_time` existe, mas precisa ser pedido de forma especial e habilitado nas configurações.
- **Custo: não confirmado por escrito.** Nenhuma das páginas lidas menciona cobrança pelo login comum com o Google, mas nenhuma diz "gratuito". O produto pago que aparece nas buscas (Identity Platform) é outro e não será usado.

## Decisão

1. **Botão "Entrar com o Google"** nas páginas de login e de cadastro, com o fluxo de login externo do ASP.NET Core Identity (`AddGoogle`, tabela `AspNetUserLogins`, que já existe desde o M1). A loja pede ao Google **só** os dados básicos (e-mail e perfil) e não guarda nenhum token do Google.
2. **Primeira vez com o Google e nenhuma conta com esse e-mail: cria a conta na hora** (decisão do autor), sem senha, ligada ao Google, e a pessoa já entra.
3. **Já existe uma conta com senha usando o mesmo e-mail: recusa** (decisão do autor), com a mensagem "Já existe uma conta com este e-mail. Entre com a senha e conecte o Google em Minha conta." A loja **nunca junta contas sozinha pelo e-mail**: como o cadastro não confirma o e-mail, alguém poderia criar antes uma conta com o e-mail da vítima e continuar entrando com a própria senha depois de a vítima conectar o Google.
4. **Conectar o Google em "Minha conta"**: quem entrou com a senha pode conectar a sua conta Google (ida e volta ao Google; a ligação só é aceita se o e-mail do Google for o mesmo da conta). Desconectar fica para depois, se fizer falta.
5. **Admin só com senha** (decisão do autor): uma conta com o papel Admin não entra pelo Google e não pode conectar o Google. O admin continua criado pelo `create-admin` (ADR 0023).
6. **Exclusão de conta de quem só tem Google** (decisão do autor, depois de saber o limite do `prompt`): confirma **digitando o próprio e-mail** no lugar da senha. Protege contra o clique por engano; não protege contra alguém usando o computador da pessoa já com a sessão aberta (o mesmo limite de voltar ao Google, que não garante pedir a senha). Quem tem senha continua confirmando com a senha (ADR 0033). A ligação com o Google (`AspNetUserLogins`) é apagada junto com a conta.
7. **Segredos e configuração:** `Authentication:Google:ClientId` e `ClientSecret` obrigatórios (a aplicação não sobe sem eles, como as chaves da Stripe); no PC em `user-secrets`, na VM no `.env`; nos testes, valores falsos (nenhum teste fala com o Google de verdade). Endereços de volta cadastrados no Google: `https://localhost:<porta>/signin-google` e `https://keycapstore.duckdns.org/signin-google`.
8. **Custo zero:** o projeto no Google Cloud é criado **sem faturamento** (sem cartão). Se o console exigir cartão para o login funcionar, o item para e é reavaliado.
9. **Textos novos em português e inglês** (ADR 0038).

## Alternativas consideradas

- **Juntar as contas sozinho pelo e-mail:** mais prático, mas abre o golpe da conta criada antes (decisão 3).
- **Pedir confirmação antes de criar a conta nova:** um clique a mais sem ganho de segurança.
- **Excluir voltando ao Google** (`select_account` + volta em até 5 min): bem mais trabalhoso e, sem como exigir a senha de novo, protege quase o mesmo que digitar o e-mail. O autor escolheu primeiro essa opção e trocou depois de saber o limite.
- **Exigir criar uma senha antes de excluir:** simples para a loja, estranho para o cliente.
- **Admin também pelo Google:** mais prático, mas qualquer problema na conta Google viraria um problema no admin.
- **Confirmar o e-mail no cadastro com senha** (resolveria o risco da decisão 3 por outro caminho): exige enviar e-mail, que a loja não faz; fica para quando houver envio de e-mail.

## Consequências

- **Positivas:** entrar sem criar senha; a loja não guarda senha de quem usa o Google; nenhuma conta é tomada pelo golpe do e-mail não confirmado.
- **Negativas / custos:** dependência de um serviço externo (se o Google estiver fora, só o login pelo Google falha); mais um segredo no `.env`; mais um caminho na exclusão de conta; quem tem conta com senha e tenta o Google pela primeira vez recebe uma recusa (com a instrução de conectar).
- **Como saber se deu errado:** duas contas para a mesma pessoa; uma conta com senha que passa a aceitar o Google sem ter sido conectada pelo dono; um admin entrando pelo Google; o console do Google pedindo cartão.
