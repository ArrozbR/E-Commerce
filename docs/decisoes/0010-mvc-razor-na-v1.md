# 0010. Frontend da v1 em ASP.NET Core MVC com Razor; SPA na v4

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D18

## Contexto

O autor domina ASP.NET Core MVC com Razor (agenda-pessoal) e nunca usou React, Angular ou Blazor em projeto. A v1 já exige aprender Stripe, domínio rico, concorrência no banco, testes de integração e CI/CD. Cada novidade no stack disputa atenção com o que diferencia o projeto.

## Decisão

Usar **MVC com Razor** na v1, com **controllers finos** (nenhuma regra de negócio na camada Web). A **Web API + SPA (React ou Angular)** fica como incremento próprio, na v4.

## Alternativas consideradas

- **Web API + React/Angular na v1:** sinaliza full-stack moderno, mas exige aprender um framework ao mesmo tempo que todo o resto, complica a autenticação (JWT ou cookies com CORS) e arrisca virar "um projeto para aprender React".
- **Blazor:** tudo em C#, mas com menos demanda de mercado e complexidades próprias, sem retorno proporcional.

## Consequências

- **Positivas:** aprendizado zero na interface; autenticação por cookie, que o autor já configura bem; toda a energia vai para o backend.
- **Negativas:** num primeiro olhar, parece "menos moderno".
- **Como saber se deu errado:** regra de negócio aparecendo em controller ou view, o que tornaria a v4 cara.
