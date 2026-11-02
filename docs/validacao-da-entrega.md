# Checklist de validação da entrega

Use este roteiro para registrar uma execução manual contra a API escolhida pelo curso. Os cenários automatizados usam HTTP simulado; este roteiro valida a integração real e a interface.

## Preparação

- [ ] Confirmar que `MugginsApiClient.DefaultBaseAddress` aponta para o Swagger atual informado pelo professor.
- [ ] Restaurar e compilar: `dotnet restore Muggins.sln` e `dotnet build Muggins.sln`.
- [ ] Executar o runner: `dotnet run --project Muggins.Engine.Tests/Muggins.Engine.Tests.csproj`.
- [ ] Iniciar a aplicação e abrir `http://127.0.0.1:5080`.
- [ ] Solicitar token, conectar e ativar os perfis necessários. Nunca incluir token ou chave de ativação na gravação.

## Fluxo contra o bot

- [ ] Preparar os perfis humano e Muggins e iniciar uma partida com alvo de 50 pontos.
- [ ] Conferir nome, mão própria, quantidade de peças do oponente, rodada, turno, pontas e placar.
- [ ] Jogar a abertura obrigatória e conferir que o bot só age quando a API lhe concede a vez.
- [ ] Confirmar que uma jogada que totalize 5, 10 ou 15 nas pontas recebe os pontos informados pela API.
- [ ] Testar uma peça dupla aberta e confirmar a contagem das duas metades expostas.
- [ ] Testar compra e passagem apenas quando a próxima ação da API autoriza.
- [ ] Acompanhar uma mudança de rodada: mão/mesa reiniciam conforme a API, sem reaproveitar peças da rodada anterior.
- [ ] Encerrar uma partida pela regra, consultar o vencedor e reiniciar com os mesmos participantes e placar zerado.
- [ ] Conferir que o bot não envia jogadas inválidas nem repete uma ação após conflito de versão.

## Fluxo online com duas pessoas

- [ ] Em dois clientes e duas contas/perfis distintos, criar e ingressar em uma sala.
- [ ] Conferir que só o anfitrião inicia a partida e que ambos veem o mesmo turno, mesa e placar.
- [ ] Confirmar que cada participante vê somente a própria mão e apenas a quantidade de peças do oponente.
- [ ] Recarregar um cliente e confirmar que a sessão recupera o estado oficial.
- [ ] Cancelar uma partida aberta e reiniciar uma partida encerrada; verificar as mensagens e o estado retornado pela API.
- [ ] Comprovar que uma ação rejeitada aparece como erro e não altera a mesa ou o placar exibidos.

## Evidência para apresentação

Registrar a versão do commit, a saída do runner automatizado e capturas sem credenciais. Na demonstração, mostrar ao menos uma partida completa contra o bot e uma interação online com o segundo cliente.
