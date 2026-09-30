# 0015. Dados pessoais mínimos, logs sem dados pessoais e política de exclusão

- **Status:** Aceito (a exclusão é implementada na v2)
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D26, §9

## Contexto

A LGPD exige finalidade e necessidade para cada dado pessoal, e dá ao titular o direito de pedir a exclusão. Ao mesmo tempo, os pedidos pagos precisam existir para fins financeiros, fiscais e de defesa em contestações. Se o endereço ficasse só na conta, uma mudança de endereço alteraria pedidos antigos.

## Decisão

- **Dados na v1:** e-mail e **hash** da senha (Identity); nome do destinatário e endereço como **snapshot** (`ShippingAddress`) dentro do `Order`, digitados no checkout. Nenhum dado de cartão.
- **Logs só com IDs**, nunca e-mail, nome ou endereço.
- **Política de exclusão (implementação na v2):** apaga a conta do Identity; mantém os pedidos com `CustomerId = null`; mantém o snapshot de nome e endereço pelo prazo legal e depois anonimiza; **bloqueia a exclusão enquanto houver pedidos em andamento**.
- **Já na v1:** `Order.CustomerId` **anulável** e endereço como snapshot, para que a v2 não precise de uma migration dolorosa.

## Alternativas consideradas

- **Endereço referenciado na conta:** altera o histórico de pedidos quando o endereço muda.
- **Apagar tudo na exclusão:** destrói registros financeiros e fiscais necessários.
- **Não apagar nada:** viola a LGPD.
- **Coletar CPF, telefone, data de nascimento:** não há finalidade na v1.

## Consequências

- **Positivas:** pouca superfície de vazamento; o pedido é um registro histórico imutável.
- **Negativas:** a exclusão e a anonimização precisam de um job (v2); a política de privacidade precisa mencionar o compartilhamento com a Stripe (checklist de produção).
- **Como saber se deu errado:** um dado pessoal aparecendo em log; uma chave estrangeira obrigatória entre `Order` e o usuário.
