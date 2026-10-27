# Muggins Engine

Motor em C# para o jogador autônomo do projeto de Dominó Ponta de Quina.
Ele recebe a mão privada e a fase do turno já mapeadas a partir do estado da API,
calcula jogadas candidatas e escolhe uma ação.

## O que está implementado

- Modelo de pedras duplo-seis, pontas da mesa, fases e ações.
- Encaixes locais à esquerda e à direita.
- Pontuação estimada pelas pontas: múltiplo de cinco vale soma ÷ 5; um duplo
  aberto conta os dois lados da pedra (por exemplo, 6|6 vale 12).
- Abertura com a pedra obrigatória informada pela API.
- Estratégia heurística determinística: priorizar pontos imediatos, estimar a melhor
  oportunidade de pontuação no próximo lance, manter opções de encaixe e descartar
  pedras de maior soma nos desempates.
- Compra e passagem só são enviadas quando a fase confirmada pela API permite.

## Limite de responsabilidade

Este projeto implementa o jogador autônomo dentro do cliente. Ele não distribui as
pedras, não estima a mão do adversário e não decide pontuação oficial, fim de rodada
ou vencedor. A API continua sendo a autoridade. A pasta `Api` contém os DTOs do
Swagger, o cliente HTTP, o mapeamento para `TurnState` e o envio de `BotAction` com
a versão do estado e uma chave de idempotência nova.

## Cliente da API

O cliente usa por padrão `https://domino-quina-api-b1-pf2261.azurewebsites.net/`.
O token de conta recebido por e-mail e a chave de ativação do jogador são segredos
distintos. Gere a chave com `MugginsApiClient.GeneratePlayerActivationKey()`, envie-a
ao ativar o jogador e guarde-a no armazenamento seguro do cliente. Nas operações da
partida, o adaptador envia o token como `Authorization: Bearer ...` e a chave no
cabeçalho `X-Jogador-Ativacao`.

```csharp
using Muggins.Engine.Api;
using Muggins.Engine.Domain;

var api = new MugginsApiClient(new HttpClient());
var activationKey = MugginsApiClient.GeneratePlayerActivationKey();

await api.ActivatePlayerAsync(token, playerId, activationKey);
Func<IReadOnlyList<PecaPartidaDto>, BoardEnds>? resolver = null;
var result = await new BotTurnCoordinator(api).RunOnceAsync(
    token,
    activationKey,
    matchId,
    playerId,
    boardEndsResolver: resolver);

// Use result.State como base; em StateConflict, peça uma nova decisão com o estado atualizado.
```

`GetUpdatesAsync` retorna `null` quando a API responde 204 (nenhuma mudança). Em
respostas de conflito, `MugginsApiException.ApiError?.EstadoAutoritativo` contém o
estado atualizado quando enviado pela API.

Para executar um ciclo de decisão por vez, use `BotTurnCoordinator.RunOnceAsync`.
O resultado indica se nenhuma ação era necessária, se a ação foi confirmada ou se
houve conflito de versão; neste último caso, `State` contém a cópia autoritativa para
uma nova decisão.

O Swagger não especifica a ordem e a orientação das pedras em `tabuleiro`. O mapeador
aceita a abertura com tabuleiro vazio, mas exige um `boardEndsResolver` para estados
com peças na mesa. A interface web registra cada jogada confirmada que ela mesma
envia, deriva as pontas dessa sequência e confere a quantidade e os valores das pedras
com a resposta da API. Se uma partida for alterada por outro cliente, o motor pausa
porque o contrato não permite reconstruir com segurança a orientação das pontas.

## Exemplo de uso

```csharp
using Muggins.Engine.Bot;
using Muggins.Engine.Domain;

var state = new TurnState(
    hand: [new Tile(6, 1), new Tile(2, 3), new Tile(0, 5)],
    ends: new BoardEnds(left: 6, right: 4),
    phase: TurnPhase.Play);

var action = new MugginsBot().ChooseAction(state);
Console.WriteLine(action);
```

O projeto tem como alvo `net8.0` e pode ser aberto diretamente como
[`Muggins.Engine.csproj`](Muggins.Engine.csproj). `EstimatedPoints` serve apenas
para a estratégia local; o placar exibido deve vir da resposta confirmada pela API.
O motor conserva se a ponta aberta é um duplo para calcular corretamente as
estimativas de jogadas, mas não altera a pontuação oficial enviada pela API.

## Testes

O projeto de testes não depende de pacotes externos. Execute-o com:

```sh
dotnet run --project ../Muggins.Engine.Tests/Muggins.Engine.Tests.csproj
```
