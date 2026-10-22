# Graph Report - Muggins  (2026-10-22)

## Corpus Check
- Corpus is ~4,822 words - fits in a single context window. You may not need a graph.

## Summary
- 315 nodes · 596 edges · 16 communities
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 36 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Bot Decision Rules
- Game State DTOs
- HTTP Test Harness
- API Request Flow
- API Turn Coordination
- Engine Concepts
- Game Setup Requests
- Player Activation
- Tile Play Requests
- Game History Events
- Turn Phase States
- API Error Handling
- Bot Turn Actions
- Player Profiles
- Project Build Configuration
- Game Session Entry

## God Nodes (most connected - your core abstractions)
1. `MugginsApiClient` - 33 edges
2. `EstadoPartidaParticipanteDto` - 28 edges
3. `Tile` - 19 edges
4. `BotAction` - 17 edges
5. `StubHandler` - 15 edges
6. `EstadoPartidaObservadorDto` - 15 edges
7. `ConfirmacaoAcaoPartidaDto` - 14 edges
8. `BoardEnds` - 14 edges
9. `TurnState` - 14 edges
10. `EventoHistoricoPartidaDto` - 13 edges

## Surprising Connections (you probably didn't know these)
- `Official Game API` --semantically_similar_to--> `Official Game API`  [INFERRED] [semantically similar]
  README.md → Muggins.Engine/README.md
- `Muggins.Engine Project` --semantically_similar_to--> `Muggins Engine`  [INFERRED] [semantically similar]
  README.md → Muggins.Engine/README.md
- `BotTurnResult` --references--> `EstadoPartidaParticipanteDto`  [EXTRACTED]
  Muggins.Engine/Api/BotTurnCoordinator.cs → Muggins.Engine/Api/ApiDtos.cs
- `BotTurnResult` --references--> `ConfirmacaoAcaoPartidaDto`  [EXTRACTED]
  Muggins.Engine/Api/BotTurnCoordinator.cs → Muggins.Engine/Api/ApiDtos.cs
- `BotTurnResult` --references--> `BotAction`  [EXTRACTED]
  Muggins.Engine/Api/BotTurnCoordinator.cs → Muggins.Engine/Domain/BotAction.cs

## Import Cycles
- None detected.

## Communities (16 total, 0 thin omitted)

### Community 0 - "Bot Decision Rules"
Cohesion: 0.06
Nodes (34): Muggins.Engine.Rules, Muggins.Engine.Bot, Muggins.Engine.Domain, Candidate, MugginsBot, BoardEnds, IsEmpty, Left (+26 more)

### Community 1 - "Game State DTOs"
Cohesion: 0.05
Nodes (42): EstadoPartidaObservadorDto, JogadorDoTurnoId, NumeroRodada, Participantes, PartidaId, Situacao, Tabuleiro, VencedorId (+34 more)

### Community 2 - "HTTP Test Harness"
Cohesion: 0.09
Nodes (7): BotTurnCoordinator, ApiIntegrationTests, CapturedRequest, StubHandler, Requests, EngineTests, TestAssert

### Community 3 - "API Request Flow"
Cohesion: 0.17
Nodes (5): ConfirmacaoAcaoPartidaDto, AcaoIdempotente, ChaveIdempotencia, Estado, MugginsApiClient

### Community 4 - "API Turn Coordination"
Cohesion: 0.09
Nodes (11): Muggins.Engine.Api, Muggins.Engine.Tests, ApiGameStateMapper, BotTurnResult, BotTurnStatus, ActionConfirmed, NoAction, StateConflict (+3 more)

### Community 5 - "Engine Concepts"
Cohesion: 0.10
Nodes (20): Account Token, Player Activation Key, Official Game API, Autonomous Player, Board Ends Resolver, BotAction, BotTurnCoordinator, Dominó Ponta de Quina (+12 more)

### Community 6 - "Game Setup Requests"
Cohesion: 0.15
Nodes (12): AcaoRequestDto, ChaveIdempotencia, JogadorId, VersaoEsperada, CriarPartidaRequestDto, JogadorId, PontuacaoAlvo, Webhook (+4 more)

### Community 7 - "Player Activation"
Cohesion: 0.20
Nodes (8): AtivarJogadorRequestDto, ChaveAtivacao, CriarJogadorRequestDto, Nome, SessaoResponseDto, UsuarioId, SolicitarTokenRequestDto, Email

### Community 8 - "Tile Play Requests"
Cohesion: 0.20
Nodes (10): JogarRequestDto, ChaveIdempotencia, JogadorId, Lado, ValorA, ValorB, VersaoEsperada, LadoJogavel (+2 more)

### Community 9 - "Game History Events"
Cohesion: 0.25
Nodes (8): EventoHistoricoPartidaDto, EventoId, JogadorId, Lado, PassouVez, PecaJogada, PontosConcedidos, RecebidoEmUtc

### Community 10 - "Turn Phase States"
Cohesion: 0.25
Nodes (7): TurnPhase, Buy, Finished, Opening, Pass, Play, Wait

### Community 11 - "API Error Handling"
Cohesion: 0.29
Nodes (6): RespostaErroDto, Codigo, EstadoAutoritativo, MugginsApiException, ApiError, RawResponse

### Community 12 - "Bot Turn Actions"
Cohesion: 0.29
Nodes (7): ProximaAcaoPartida, Aguardar, Comprar, Encerrada, Jogar, JogarAbertura, Passar

### Community 13 - "Player Profiles"
Cohesion: 0.33
Nodes (5): PerfilJogadorDto, AtivadoEmUtc, Ativo, JogadorId, Nome

### Community 14 - "Project Build Configuration"
Cohesion: 0.33
Nodes (4): net8.0, Microsoft.NET.Sdk, net8.0, Microsoft.NET.Sdk

### Community 15 - "Game Session Entry"
Cohesion: 0.40
Nodes (5): IngressarRequestDto, ChaveIdempotencia, JogadorId, VersaoEsperada, Webhook

## Knowledge Gaps
- **130 isolated node(s):** `Requests`, `net8.0`, `Microsoft.NET.Sdk`, `Email`, `UsuarioId` (+125 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 164 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EstadoPartidaParticipanteDto` connect `Game State DTOs` to `HTTP Test Harness`, `API Request Flow`, `API Turn Coordination`, `Game Setup Requests`, `Player Activation`, `API Error Handling`, `Bot Turn Actions`?**
  _High betweenness centrality (0.181) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `MugginsApiClient` (e.g. with `.CoordinatorReturnsAuthoritativeStateOnConflictAsync()` and `.CoordinatorSkipsSubmissionWhenWaitingAsync()`) actually correct?**
  _`MugginsApiClient` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Requests`, `net8.0`, `Microsoft.NET.Sdk` to the rest of the system?**
  _130 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Bot Decision Rules` be split into smaller, more focused modules?**
  _Cohesion score 0.05875706214689266 - nodes in this community are weakly interconnected._
- **Why does `MugginsApiClient` connect `API Request Flow` to `HTTP Test Harness`, `API Turn Coordination`?**
  _High betweenness centrality (0.104) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `Tile` (e.g. with `.ToTurnState()` and `.ApiJsonAndBoardMappingAsync()`) actually correct?**
  _`Tile` has 4 INFERRED edges - model-reasoned connections that need verification._
- **Should `Game State DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.04927536231884058 - nodes in this community are weakly interconnected._

## Graph Health Check

Warning: 12 dangling-endpoint edges; 43 directed and 47 undirected edges collapse onto repeated endpoint pairs. The graph remains usable, but these relationships may be incomplete or collapsed.

## Token Accounting

Graphify recorded 0 input and 0 output tokens. The delegated semantic extraction interface did not expose actual usage, so these recorded totals exclude semantic-model usage and are not a complete usage estimate.
