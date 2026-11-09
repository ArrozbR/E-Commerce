# 0052. Minha conta: e-mails de novidades (kit novo e pré-venda) só com consentimento, e troca de e-mail confirmada no endereço novo

- **Status:** Aceito
- **Data:** 2026-11-09
- **Relacionado:** ADR 0049 (e-mails da loja), ADR 0050 (esqueci minha senha), ADR 0051 (avise-me), ADR 0039 (login com Google), ADR 0033 (exclusão de conta), ADR 0015 (dados pessoais)

## Contexto

Item 4 da V4: na "Minha conta", ligar e desligar os e-mails de novidades e trocar o e-mail.

Conferido na documentação do Gmail (09/11, *Email sender guidelines* e o FAQ):
- mensagens de marketing e de inscrição precisam de **descadastro com um clique**: os cabeçalhos `List-Unsubscribe` (uma URL HTTPS) e `List-Unsubscribe-Post`, como na RFC 8058, mais um link visível no corpo;
- o descadastro deve valer **em até 48 horas**;
- mensagens transacionais (troca de senha, confirmações) ficam fora dessa regra;
- o FAQ diz que a cobrança disso fica mais rígida a partir de novembro de 2025.

## Decisão

**E-mails de novidades**

1. **O que são** (decisão do autor): avisos automáticos de **kit novo à venda** e de **pré-venda aberta**. O admin não escreve nada.
2. **Consentimento explícito** (decisão do autor):
   - começa **desligado**;
   - no cadastro há uma caixa **desmarcada** ("Quero receber e-mails de kits novos e pré-vendas"), e a "Minha conta" tem a chave para ligar e desligar;
   - o consentimento fica numa tabela própria (`newsletter_subscriptions`: cliente, idioma e data), que entra no "baixar meus dados" e é apagada com a conta.
3. **Quando sai o aviso:** um job confere os produtos ativos que **ainda não foram anunciados** e estão à venda (estoque, ou pré-venda aberta). Para cada um, numa transação:
   - anota um e-mail para cada inscrito (`NewKit` ou `PreOrderOpened`, com o kit);
   - marca o produto como **anunciado** (tabela `product_announcements`, com o kit como chave; uma segunda rodada que tente anunciar o mesmo kit é recusada pelo banco e desfeita inteira). Não muda nada na entidade `Product`.

   Os produtos que já existem recebem a marca na migration, então ninguém recebe aviso de um kit antigo. Na hora do envio, o e-mail é descartado se a pessoa desligou as novidades depois do anúncio. Reabrir as datas de uma pré-venda não anuncia de novo.
4. **Descadastro:**
   - todo e-mail de novidades traz um link `/Newsletter/Unsubscribe?token=...` e os cabeçalhos `List-Unsubscribe` e `List-Unsubscribe-Post: List-Unsubscribe=One-Click`;
   - o token é o id do cliente **protegido pelo Data Protection** (assinado, ninguém consegue montar o de outra pessoa) e não vence;
   - o `POST` nesse endereço desliga na hora, sem login e sem antiforgery, porque é o leitor de e-mail que faz esse `POST` (RFC 8058);
   - o `GET` mostra uma página com o botão de confirmar. Um `GET` não muda nada, porque leitores de e-mail e antivírus abrem links sozinhos.
5. **Limite do Gmail:** são 500 envios por dia (ADR 0049), e o job manda no máximo 20 por minuto. Com mais de ~400 inscritos, um único anúncio passaria do limite do dia. Para um portfólio isso não acontece, mas se acontecer o sinal está anotado.

**Troca de e-mail**

6. **Só para contas com senha** (decisão do autor). A conta só do Google continua com o e-mail do Google (ADR 0039). O **admin** também não troca pelo site, como a senha (ADR 0050).
7. **Como funciona** (decisão do autor):
   - a pessoa digita o e-mail novo e a **senha atual**, que conta para o bloqueio, como na troca de senha;
   - o e-mail novo recebe um **link de confirmação** de 30 minutos (`GenerateChangeEmailTokenAsync`, o mesmo prazo do ADR 0050);
   - a conta só muda quando o link é aberto: o e-mail e o nome de login juntos, numa transação;
   - o *security stamp* muda e as outras sessões caem;
   - o e-mail **antigo** recebe um aviso de que o e-mail da conta foi trocado.
8. **O endereço de destino fica guardado na caixa de saída.** O link de confirmação vai para o e-mail novo, e o aviso vai para o antigo, ou seja, nos dois casos não é o e-mail atual da conta. Por isso a caixa de saída ganha a coluna `ToAddress`, usada só nesses dois tipos. Ela é apagada com a limpeza de 30 dias (ADR 0049). O link continua sendo gerado na hora do envio.
9. **E-mail já em uso:** o pedido é aceito, mas a confirmação recusa com "este e-mail já está em uso por outra conta". Para descobrir isso, seria preciso entrar na conta e digitar a senha, então o risco de alguém usar a tela para descobrir quais e-mails têm conta é pequeno.

## Alternativas consideradas

- **Só a chave de consentimento, sem envio:** mais rápido, mas a chave não faria nada.
- **Newsletter escrita pelo admin:** dá mais controle, mas exige editor, prévia e envio em lote; fica para depois, se fizer falta.
- **Novidades ligadas no cadastro:** seria consentimento presumido, arriscado pela LGPD e pelas regras do Gmail.
- **Descadastro só com login:** a regra do Gmail exige um clique, sem login.
- **Trocar o e-mail na hora:** um erro de digitação trancaria a pessoa fora da conta.
- **Não avisar o e-mail antigo:** quem invadisse a conta trocaria o e-mail sem o dono saber.
- **Guardar a troca pendente numa tabela própria:** também funciona, mas a coluna na caixa de saída resolve os dois e-mails com a limpeza que já existe.

## Consequências

- **Positivas:**
  - quem quer fica sabendo de kits novos e pré-vendas;
  - o descadastro segue a regra do Gmail;
  - a troca de e-mail não tranca ninguém fora da conta e avisa o dono.
- **Negativas / custos:**
  - tabelas e colunas novas (`newsletter_subscriptions`, `product_announcements`, `outbox_emails.ToAddress`);
  - o endereço novo e o antigo ficam até 30 dias na caixa de saída;
  - mais páginas e textos nos dois idiomas;
  - o limite de 500 por dia do Gmail.
- **Como saber se deu errado:**
  - um aviso de kit antigo depois do deploy;
  - um e-mail de novidades sem o link ou sem os cabeçalhos;
  - um descadastro que não desliga;
  - a troca de e-mail acontecendo antes do link;
  - o e-mail antigo sem aviso;
  - alguém conseguindo descadastrar outra pessoa.
