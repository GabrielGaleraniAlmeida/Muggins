(() => {
  const $ = (selector) => document.querySelector(selector);
  const page = document.body.dataset.page;
  const errorMessages = {
    NAO_AUTENTICADO: "Token inválido, ausente ou revogado. Conecte novamente.",
    JOGADOR_JA_ATIVO: "Este perfil está reservado em outro cliente. Libere-o lá ou prepare um perfil novo.",
    CONFLITO_DE_ESTADO: "A partida mudou. O estado foi atualizado; tente novamente.",
    FORA_DO_TURNO: "Ainda não é a sua vez.",
    PECA_AUSENTE: "A pedra escolhida não está na sua mão.",
    ENCAIXE_INVALIDO: "Essa pedra não encaixa nessa ponta.",
    PASSAGEM_NAO_PERMITIDA: "A API não permite passar enquanto houver uma jogada ou compra possível.",
    ESTADO_INVALIDO: "A ação não combina com a fase atual da partida."
  };

  async function callApi(path, { method = "GET", body } = {}) {
    const response = await fetch(path, {
      method,
      credentials: "same-origin",
      headers: body === undefined ? {} : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    const payload = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) {
      const code = payload?.message || `HTTP ${response.status}`;
      throw new Error(errorMessages[code] || code);
    }
    return payload;
  }

  function setBusy(button, busy, label) {
    if (!button) return;
    if (busy) {
      button.dataset.originalText = button.textContent;
      button.dataset.originalDisabled = String(button.disabled);
      button.textContent = label || "Aguarde…";
      button.disabled = true;
    } else {
      button.textContent = button.dataset.originalText || button.textContent;
      button.disabled = button.dataset.originalDisabled === "true";
      delete button.dataset.originalText;
      delete button.dataset.originalDisabled;
    }
  }

  if (page === "lobby") initializeLobby();
  if (page === "match") initializeMatch();

  function initializeLobby() {
    const overlay = $("#auth-overlay");
    const emailForm = $("#email-form");
    const tokenForm = $("#token-form");
    const authMessage = $("#auth-message");

    $("#connect-open").addEventListener("click", () => {
      overlay.hidden = false;
      authMessage.textContent = "";
      emailForm.hidden = false;
      tokenForm.hidden = true;
      $("#login-email").focus();
    });
    $("#auth-close").addEventListener("click", () => { overlay.hidden = true; });
    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) overlay.hidden = true;
    });

    emailForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = emailForm.querySelector("button[type=submit]");
      setBusy(button, true, "Enviando…");
      authMessage.textContent = "";
      try {
        await callApi("/app-api/auth/request-token", {
          method: "POST",
          body: { email: $("#login-email").value.trim() }
        });
        emailForm.hidden = true;
        tokenForm.hidden = false;
        authMessage.textContent = "Se o e-mail for válido, o token chegará na sua caixa de entrada.";
        $("#access-token").focus();
      } catch (error) {
        authMessage.textContent = error.message;
      } finally {
        setBusy(button, false);
      }
    });

    tokenForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = tokenForm.querySelector("button[type=submit]");
      setBusy(button, true, "Conectando…");
      authMessage.textContent = "";
      try {
        await callApi("/app-api/auth/connect", {
          method: "POST",
          body: { token: $("#access-token").value.trim() }
        });
        overlay.hidden = true;
        $("#access-token").value = "";
        await loadLobby();
      } catch (error) {
        authMessage.textContent = error.message;
      } finally {
        setBusy(button, false);
      }
    });

    $("#prepare-human").addEventListener("click", () => preparePlayer("human", "Você", $("#prepare-human")));
    $("#prepare-bot").addEventListener("click", () => preparePlayer("bot", "Muggins", $("#prepare-bot")));
    $("#start-match").addEventListener("click", startMatch);
    $("#create-online").addEventListener("click", createOnlineMatch);
    $("#join-online").addEventListener("click", joinOnlineMatch);
    $("#join-room-code").addEventListener("keydown", (event) => {
      if (event.key === "Enter") joinOnlineMatch();
    });
    $("#start-online").addEventListener("click", startOnlineMatch);
    $("#copy-room-code").addEventListener("click", copyRoomCode);
    $("#room-list").addEventListener("click", (event) => {
      const joinButton = event.target.closest("button[data-join-match]");
      if (!joinButton) return;
      $("#join-room-code").value = joinButton.dataset.joinMatch;
      joinOnlineMatch();
    });
    $("#disconnect").addEventListener("click", disconnect);
    loadLobby();
    window.setInterval(loadLobby, 3000);

    async function preparePlayer(role, name, button) {
      setBusy(button, true, "Preparando…");
      setLobbyMessage(`Preparando o perfil ${name}…`);
      try {
        await callApi("/app-api/players/prepare", { method: "POST", body: { role, name } });
      } catch (error) {
        setLobbyMessage(error.message);
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function startMatch() {
      const button = $("#start-match");
      setBusy(button, true, "Abrindo mesa…");
      setLobbyMessage("Criando a partida e distribuindo as pedras pela API…");
      try {
        await callApi("/app-api/matches/vs-bot", {
          method: "POST",
          body: { targetScore: Number($("#target-score").value) }
        });
        window.location.href = "/partida.html";
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
      }
    }

    async function createOnlineMatch() {
      const button = $("#create-online");
      setBusy(button, true, "Criando sala…");
      setLobbyMessage("Abrindo uma sala para outro jogador…");
      try {
        await callApi("/app-api/matches/online/create", {
          method: "POST",
          body: { targetScore: Number($("#target-score").value) }
        });
        await loadLobby();
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function joinOnlineMatch() {
      const code = $("#join-room-code").value.trim();
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(code)) {
        setLobbyMessage("Cole o código completo da sala.");
        return;
      }

      const button = $("#join-online");
      setBusy(button, true, "Entrando…");
      setLobbyMessage("Entrando na sala pela API…");
      try {
        await callApi("/app-api/matches/online/join", {
          method: "POST",
          body: { matchId: code }
        });
        window.location.href = "/partida.html";
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function startOnlineMatch() {
      const button = $("#start-online");
      setBusy(button, true, "Iniciando…");
      setLobbyMessage("Iniciando a partida pela API…");
      try {
        await callApi("/app-api/matches/online/start", { method: "POST", body: {} });
        window.location.href = "/partida.html";
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function copyRoomCode() {
      const code = $("#online-room-code").textContent.trim();
      try {
        await navigator.clipboard.writeText(code);
        $("#copy-room-code").textContent = "Código copiado";
      } catch {
        window.prompt("Copie o código da sala:", code);
      }
    }

    async function disconnect() {
      if (!window.confirm("Desconectar também libera seus perfis ativos na API?")) return;
      const button = $("#disconnect");
      setBusy(button, true, "Liberando…");
      try {
        await callApi("/app-api/auth/disconnect", { method: "POST", body: {} });
        await loadLobby();
      } catch (error) {
        setLobbyMessage(error.message);
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function loadLobby() {
      try {
        const lobby = await callApi("/app-api/lobby");
        renderLobby(lobby);
      } catch (error) {
        setLobbyMessage(error.message);
      }
    }

    function renderLobby(lobby) {
      const connected = lobby.connected;
      const humanReady = Boolean(lobby.humanPlayer?.ready);
      const botReady = Boolean(lobby.botPlayer?.ready);
      const hasMatch = Boolean(lobby.activeMatchId);
      const onlineMatch = lobby.matchMode === "Online" && hasMatch;
      $("#profile-name").textContent = lobby.humanPlayer?.name || "Você";
      $("#profile-status").textContent = connected ? "Conta FIAP conectada" : "Conta não conectada";
      $("#disclaimer").textContent = connected
        ? "Motor Muggins conectado à API"
        : "Prévia visual da mesa · conecte-se para jogar";
      $("#human-profile").textContent = lobby.humanPlayer
        ? `Você: ${lobby.humanPlayer.name}${humanReady ? " · pronto" : " · perfil sem chave local"}`
        : "Você: não preparado";
      $("#bot-profile").textContent = lobby.botPlayer
        ? `Muggins: ${lobby.botPlayer.name}${botReady ? " · pronto" : " · perfil sem chave local"}`
        : "Muggins: não preparado";

      $("#connect-open").hidden = connected;
      $("#prepare-human").hidden = !connected || humanReady;
      $("#prepare-bot").hidden = !connected || botReady;
      $("#start-match").hidden = hasMatch;
      $("#start-match").disabled = !lobby.canStartMachineMatch || hasMatch;
      $("#create-online").hidden = hasMatch;
      $("#create-online").disabled = !connected || !humanReady || hasMatch;
      $("#join-room-code").disabled = !connected || !humanReady || hasMatch;
      $("#join-online").disabled = !connected || !humanReady || hasMatch;
      $("#online-room").hidden = !onlineMatch;
      $("#online-room-code").textContent = lobby.activeMatchId || "";
      $("#online-room-status").textContent = lobby.opponentName
        ? `${lobby.opponentName} entrou na sala. ${lobby.matchParticipants}/2 jogadores conectados.`
        : "Sala criada. Compartilhe este código e aguarde o outro jogador entrar.";
      $("#start-online").hidden = !lobby.canStartOnlineMatch;
      $("#start-online").disabled = !lobby.canStartOnlineMatch;
      $("#resume-match").hidden = !hasMatch;
      $("#resume-match").textContent = onlineMatch ? "Abrir partida online ›" : "Continuar partida ›";
      $("#target-score").disabled = hasMatch;
      if (hasMatch) $("#target-score").value = String(lobby.targetScore);
      $("#disconnect").hidden = !connected;

      const roomList = $("#room-list");
      roomList.replaceChildren();
      const row = document.createElement("div");
      row.className = "room-row selected";
      row.setAttribute("aria-current", "true");
      row.innerHTML = '<svg class="people-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><circle cx="9" cy="8" r="3.2"/><circle cx="17" cy="9" r="2.5"/><path d="M2.7 18.6c.3-3.2 2.4-5.1 6.3-5.1s6 1.9 6.3 5.1v1H2.7zm12.1-4.4c3.8-.6 6.3 1.1 6.5 4.3h-4.7c-.1-1.7-.7-3.2-1.8-4.3"/></svg><span>1/2</span><span class="room-separator"></span><span class="room-target"></span><span class="chevron">›</span>';
      row.querySelector(".room-target").textContent = `Contra Muggins · ${lobby.targetScore} pts`;
      roomList.append(row);

      for (const match of lobby.waitingMatches || []) {
        const waitingRow = document.createElement("div");
        waitingRow.className = "room-row";
        waitingRow.innerHTML = '<svg class="people-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><circle cx="9" cy="8" r="3.2"/><circle cx="17" cy="9" r="2.5"/><path d="M2.7 18.6c.3-3.2 2.4-5.1 6.3-5.1s6 1.9 6.3 5.1v1H2.7zm12.1-4.4c3.8-.6 6.3 1.1 6.5 4.3h-4.7c-.1-1.7-.7-3.2-1.8-4.3"/></svg><span>1/2</span><span class="room-separator"></span><span class="room-target"></span><button class="small-action" type="button">Entrar</button>';
        waitingRow.querySelector(".room-target").textContent = `Online · ${match.pontuacaoAlvo} pts · ${match.partidaId.slice(0, 8)}`;
        const joinButton = waitingRow.querySelector("button");
        joinButton.dataset.joinMatch = match.partidaId;
        joinButton.disabled = !connected || !humanReady || hasMatch;
        joinButton.setAttribute("aria-label", `Entrar na sala ${match.partidaId}`);
        roomList.append(waitingRow);
      }

      if (hasMatch && onlineMatch && lobby.isMatchHost && !lobby.canStartOnlineMatch) {
        setLobbyMessage("Compartilhe o código da sala. O início será liberado quando o outro jogador entrar.");
      } else if (hasMatch && onlineMatch && lobby.canStartOnlineMatch) {
        setLobbyMessage("Os dois jogadores estão na sala. Inicie a partida quando estiverem prontos.");
      } else if (hasMatch) {
        setLobbyMessage("Há uma partida em andamento nesta sessão. Continue de onde parou.");
      } else if (!connected) {
        setLobbyMessage("Conecte sua conta FIAP para preparar os perfis e abrir uma partida.");
      } else if (!humanReady) {
        setLobbyMessage(`Prepare seu perfil para jogar. ${lobby.waitingMatches.length} sala(s) aguardando outro jogador.`);
      } else if (!botReady) {
        setLobbyMessage(`Seu perfil está pronto para jogar online. Prepare o perfil Muggins para enfrentar o computador.`);
      } else {
        setLobbyMessage(`Perfis prontos. ${lobby.waitingMatches.length} partida(s) de outros jogadores aguardando na API.`);
      }
    }

    function setLobbyMessage(message) {
      $("#lobby-status").textContent = message;
    }
  }

  function initializeMatch() {
    let selectedIndex = null;
    let currentMatch = null;
    let loading = false;
    let actionInProgress = false;
    let scoreNoticeTimer = null;
    const hand = $("#player-hand");
    const message = $("#game-message");
    const leftButton = $("#play-left");
    const rightButton = $("#play-right");
    const buyButton = $("#buy");
    const passButton = $("#pass");
    const finishButton = $("#finish-match");
    const restartButton = $("#restart-match");

    leftButton.addEventListener("click", () => play("left"));
    rightButton.addEventListener("click", () => play("right"));
    buyButton.addEventListener("click", () => submitAction("buy"));
    passButton.addEventListener("click", () => submitAction("pass"));
    finishButton.addEventListener("click", () => {
      if (currentMatch?.phase === "Finished") window.location.href = "/";
    });
    restartButton.addEventListener("click", restartMatch);
    window.addEventListener("resize", () => {
      if (currentMatch) renderBoard(currentMatch);
    });
    hand.addEventListener("click", (event) => {
      const tileButton = event.target.closest("button[data-index]");
      if (!tileButton || !currentMatch?.isHumanTurn) return;
      selectedIndex = Number(tileButton.dataset.index);
      renderMatch(currentMatch);
    });

    refreshMatch();
    window.setInterval(refreshMatch, 1600);

    async function refreshMatch() {
      if (loading || actionInProgress) return;
      loading = true;
      try {
        const updatedMatch = await callApi("/app-api/match");
        showScoreNotice(currentMatch, updatedMatch);
        currentMatch = updatedMatch;
        renderMatch(currentMatch);
        setMessage("");
      } catch (error) {
        setMessage(error.message);
        $("#sync-indicator").textContent = "!";
        $("#sync-label").textContent = "Sem sincronização";
        $("#sync-indicator").parentElement.style.background = "conic-gradient(#ff8e8e 0deg 360deg)";
        disableActions();
      } finally {
        loading = false;
      }
    }

    async function play(side) {
      if (!currentMatch?.isHumanTurn || selectedIndex === null) return;
      const tile = currentMatch.hand[selectedIndex];
      if (!tile) return;
      await submitAction("play", { valueA: tile.valorA, valueB: tile.valorB, side });
    }

    async function submitAction(kind, details = {}) {
      if (actionInProgress) return;
      actionInProgress = true;
      disableActions();
      setMessage(kind === "play" ? "Enviando sua jogada…" : "Enviando ação…");
      try {
        const updatedMatch = await callApi("/app-api/match/action", {
          method: "POST",
          body: { kind, ...details }
        });
        showScoreNotice(currentMatch, updatedMatch);
        currentMatch = updatedMatch;
        selectedIndex = null;
        renderMatch(currentMatch);
        setMessage("");
      } catch (error) {
        setMessage(error.message);
        actionInProgress = false;
        await refreshMatch();
      } finally {
        actionInProgress = false;
        if (currentMatch) renderMatch(currentMatch);
      }
    }

    async function restartMatch() {
      if (actionInProgress || currentMatch?.phase !== "Finished") return;
      if (!window.confirm("Criar uma nova partida com a mesma pontuação-alvo?")) return;

      actionInProgress = true;
      setBusy(restartButton, true, "Abrindo…");
      setMessage("Criando uma nova partida…");
      let restartError = null;
      try {
        await callApi("/app-api/matches/vs-bot", {
          method: "POST",
          body: { targetScore: currentMatch.targetScore }
        });
        window.location.replace("/partida.html");
      } catch (error) {
        restartError = error.message;
      } finally {
        actionInProgress = false;
        setBusy(restartButton, false);
        if (currentMatch) renderMatch(currentMatch);
        if (restartError) setMessage(restartError);
      }
    }

    function renderMatch(state) {
      if (!state) return;
      if (selectedIndex !== null && selectedIndex >= state.hand.length) selectedIndex = null;
      const isFinished = state.phase === "Finished";
      $("#match-label").textContent = `Rodada ${state.round} · até ${state.targetScore} pontos`;
      $("#opponent-label").textContent = `${state.botName || "Muggins"} · ${state.botScore} pts`;
      $("#opponent-count").textContent = state.botTileCount;
      $("#human-name").textContent = state.humanName || "Você";
      $("#human-score").textContent = state.humanScore;
      $("#human-count").textContent = state.humanTileCount;
      const boardTiles = state.board || [];
      const singleOpeningDouble = boardTiles.length === 1 && boardTiles[0].isDouble;
      const leftEndCount = state.leftEnd === null
        ? 0
        : state.leftEnd * (boardTiles.length > 1 && boardTiles[0]?.isDouble ? 2 : 1);
      const rightEndCount = state.rightEnd === null
        ? 0
        : state.rightEnd * (boardTiles.length > 1 && boardTiles[boardTiles.length - 1]?.isDouble ? 2 : 1);
      $("#ends-total").textContent = String(state.endsTotal ?? leftEndCount + rightEndCount);
      $("#ends-values").textContent = singleOpeningDouble
        ? `${state.leftEnd} + ${state.rightEnd}`
        : `${leftEndCount} + ${rightEndCount}`;
      $("#turn-indicator").textContent = turnText(state);
      $("#sync-indicator").textContent = "●";
      $("#sync-label").textContent = "Estado confirmado pela API";
      $("#sync-indicator").parentElement.style.background = "conic-gradient(#66d99b 0deg 360deg)";
      finishButton.disabled = !isFinished || actionInProgress;
      finishButton.title = isFinished
        ? "Voltar à sala após o encerramento confirmado pela API."
        : "A API atual não permite encerrar ou abandonar uma partida em andamento.";
      restartButton.disabled = !isFinished || actionInProgress;
      restartButton.title = isFinished
        ? "Criar uma nova partida com a mesma pontuação-alvo."
        : "Disponível quando a partida terminar pelas regras da API.";
      $("#match-controls-note").textContent = isFinished
        ? "Partida encerrada pela API"
        : "Encerramento depende das regras da API";

      const opponentHand = $("#opponent-hand");
      opponentHand.replaceChildren();
      const backCount = Math.min(7, state.botTileCount);
      for (let index = 0; index < backCount; index += 1) {
        const back = document.createElement("span");
        back.className = "back-tile";
        opponentHand.append(back);
      }
      opponentHand.setAttribute("aria-label", `${state.botTileCount} peças do oponente, viradas para baixo`);

      renderBoard(state);

      hand.replaceChildren();
      state.hand.forEach((piece, index) => {
        const button = createDomino(piece.valorA, piece.valorB, true, true);
        button.dataset.index = String(index);
        button.dataset.a = String(piece.valorA);
        button.dataset.b = String(piece.valorB);
        button.classList.toggle("selected", selectedIndex === index);
        button.setAttribute("aria-pressed", selectedIndex === index ? "true" : "false");
        hand.append(button);
      });

      const selected = selectedIndex === null ? null : state.hand[selectedIndex];
      $("#selected-caption").textContent = selected
        ? `Selecionada: ${selected.valorA} | ${selected.valorB}`
        : state.phase === "Opening" ? `Abertura obrigatória: ${tileText(state.requiredOpeningTile)}` : "Selecione uma pedra";

      const canSelect = state.isHumanTurn && ["Opening", "Play"].includes(state.phase);
      hand.querySelectorAll("button").forEach((button) => { button.disabled = !canSelect || actionInProgress; });
      const selectedTile = selected ? [selected.valorA, selected.valorB] : null;
      const opening = state.requiredOpeningTile;
      const isRequiredOpening = selectedTile && opening && sameTile(selectedTile, [opening.valorA, opening.valorB]);
      const leftFits = selectedTile && state.leftEnd !== null && selectedTile.includes(state.leftEnd);
      const rightFits = selectedTile && state.rightEnd !== null && selectedTile.includes(state.rightEnd);
      leftButton.disabled = actionInProgress || !state.isHumanTurn || !(state.phase === "Play" && leftFits || state.phase === "Opening" && isRequiredOpening);
      rightButton.disabled = actionInProgress || !state.isHumanTurn || state.phase !== "Play" || !rightFits;
      leftButton.textContent = state.phase === "Opening" ? "Abrir mesa" : "← Esquerda";
      buyButton.disabled = actionInProgress || !state.isHumanTurn || state.phase !== "Buy";
      passButton.disabled = actionInProgress || !state.isHumanTurn || state.phase !== "Pass";
      leftButton.classList.toggle("target-action", !leftButton.disabled);
      rightButton.classList.toggle("target-action", !rightButton.disabled);
      buyButton.classList.toggle("target-action", !buyButton.disabled);
      passButton.classList.toggle("target-action", !passButton.disabled);

      if (state.phase === "Finished") {
        const winner = state.winnerName || "Empate";
        setMessage(`Partida encerrada · ${winner}`);
      }
    }

    function renderBoard(state) {
      const board = $("#board-chain");
      const boardTiles = state.board || [];
      board.replaceChildren();
      board.setAttribute("aria-label", `${boardTiles.length} peça(s) no tabuleiro`);
      if (boardTiles.length === 0) {
        board.style.transform = "translate(-50%, -50%)";
        return;
      }

      const boardZone = board.closest(".board-zone");
      const boardWidth = board.clientWidth || (boardZone?.clientWidth || window.innerWidth) * 0.94;
      const boardHeight = boardZone?.clientHeight || window.innerHeight * 0.5;
      const requestedOpeningIndex = Number.isInteger(state.openingTileIndex)
        ? state.openingTileIndex
        : Math.floor((boardTiles.length - 1) / 2);
      const openingIndex = Math.max(0, Math.min(boardTiles.length - 1, requestedOpeningIndex));
      const tileGap = 0;
      const rowGap = 0;
      const availableHeight = Math.max(90, boardHeight * 0.84);
      const isDoubleTile = (piece) => piece.leftValue === piece.rightValue;
      let maxTileWidth = Math.min(116, boardWidth);
      if (boardTiles.length > 10) {
        // Long runs use the reference's roughly eleven-piece span; each vertical turn continues below its row.
        const maxTilesAtMinimumSize = Math.max(1,
          Math.floor((boardWidth + tileGap) / (38 + tileGap)));
        const targetTilesPerRun = Math.min(11, maxTilesAtMinimumSize);
        maxTileWidth = Math.min(maxTileWidth,
          (boardWidth - tileGap * (targetTilesPerRun - 1)) / targetTilesPerRun);
      }
      const buildSnakeLayout = (candidateWidth) => {
        const candidateHeight = candidateWidth * 66 / 116;
        const lines = [];
        const turns = [];
        const horizontalDoubleIndices = new Set();
        const isVerticalDouble = (tileIndex) =>
          isDoubleTile(boardTiles[tileIndex]) && !horizontalDoubleIndices.has(tileIndex);
        const pieceWidth = (tileIndex) => isVerticalDouble(tileIndex) ? candidateHeight : candidateWidth;
        const pieceHeight = (tileIndex) => isVerticalDouble(tileIndex) ? candidateWidth : candidateHeight;
        let index = 0;
        let direction = 1;
        let openingPrefixWidth = 0;
        for (let prefixIndex = 0; prefixIndex < openingIndex; prefixIndex += 1) {
          openingPrefixWidth += pieceWidth(prefixIndex) + tileGap;
        }
        let anchor = Math.max(0, Math.min(
          boardWidth,
          boardWidth / 2 - openingPrefixWidth - pieceWidth(openingIndex) / 2
        ));
        let nextLineTop = 0;
        let layoutHeight = 0;

        while (index < boardTiles.length) {
          const available = direction > 0 ? boardWidth - anchor : anchor;
          const remainingCount = boardTiles.length - index;
          let remainingWidth = tileGap * (remainingCount - 1);
          for (let remainingIndex = index; remainingIndex < boardTiles.length; remainingIndex += 1) {
            remainingWidth += pieceWidth(remainingIndex);
          }
          const indices = [];
          let lineWidth = 0;
          let lineHeight = candidateHeight;

          if (remainingWidth <= available) {
            while (index < boardTiles.length) {
              const currentPieceWidth = pieceWidth(index);
              lineWidth += indices.length > 0 ? tileGap + currentPieceWidth : currentPieceWidth;
              lineHeight = Math.max(lineHeight, pieceHeight(index));
              indices.push(index);
              index += 1;
            }
          } else {
            const turnOverhang = Math.max(0, candidateHeight / 2 - candidateWidth / 4);
            const turnReserve = Math.max(turnOverhang, candidateWidth / 4);
            const lineBudget = available - turnReserve;
            while (index < boardTiles.length) {
              const currentPieceWidth = pieceWidth(index);
              const nextWidth = indices.length > 0 ? tileGap + currentPieceWidth : currentPieceWidth;
              if (lineWidth + nextWidth > lineBudget) break;
              lineWidth += nextWidth;
              lineHeight = Math.max(lineHeight, pieceHeight(index));
              indices.push(index);
              index += 1;
            }
            if (indices.length === 0 || index + 1 >= boardTiles.length) return null;
          }

          const lineLeft = direction > 0 ? anchor : anchor - lineWidth;
          const lineContentTop = nextLineTop;
          const lineTop = lineContentTop - (lineHeight - candidateHeight) / 2;
          lines.push({ indices, left: lineLeft, anchor, width: lineWidth, height: lineHeight, direction, top: lineTop, contentTop: lineContentTop });
          if (index >= boardTiles.length) {
            layoutHeight = lineTop + lineHeight;
            break;
          }

          const turnIndex = index;
          const terminalIndex = indices[indices.length - 1];
          const terminalOffset = isVerticalDouble(terminalIndex) ? candidateHeight / 2 : candidateWidth / 4;
          const turnCenter = direction > 0
            ? lineLeft + lineWidth - terminalOffset
            : lineLeft + terminalOffset;
          const turnLeft = turnCenter - candidateHeight / 2;
          const turnTop = lineContentTop + candidateHeight + rowGap;
          turns.push({ index: turnIndex, left: turnLeft, top: turnTop });
          index += 1;
          const nextPieceIsDouble = isDoubleTile(boardTiles[index]);
          if (nextPieceIsDouble) horizontalDoubleIndices.add(index);
          const nextPieceOffset = nextPieceIsDouble ? candidateWidth / 2 : candidateWidth / 4;
          anchor = direction > 0
            ? turnCenter + nextPieceOffset
            : turnCenter - nextPieceOffset;
          direction *= -1;
          nextLineTop = turnTop + candidateWidth + rowGap;
        }

        const verticalOffset = Math.max(0, -lines[0].top);
        if (verticalOffset > 0) {
          for (const line of lines) {
            line.top += verticalOffset;
            line.contentTop += verticalOffset;
          }
          for (const turn of turns) turn.top += verticalOffset;
          layoutHeight += verticalOffset;
        }

        return {
          lines,
          turns,
          height: layoutHeight,
          tileHeight: candidateHeight,
          horizontalDoubleIndices: [...horizontalDoubleIndices]
        };
      };

      let layout = null;
      let tileWidth = 38;
      let bestLayoutScore = -1;
      for (let candidateWidth = maxTileWidth; candidateWidth >= 38; candidateWidth -= 0.5) {
        const candidateLayout = buildSnakeLayout(candidateWidth);
        if (!candidateLayout || candidateLayout.height > availableHeight) continue;

        const rowsToScore = candidateLayout.lines.length > 1
          ? candidateLayout.lines.slice(0, -1)
          : candidateLayout.lines;
        const averageFill = rowsToScore.reduce((sum, line) =>
          sum + Math.min(1, line.width / boardWidth), 0) / rowsToScore.length;
        const layoutScore = candidateWidth * averageFill * averageFill;
        if (layoutScore > bestLayoutScore) {
          bestLayoutScore = layoutScore;
          layout = candidateLayout;
          tileWidth = candidateWidth;
        }
      }

      if (!layout) {
        const fallbackHeight = tileWidth * 66 / 116;
        const indices = boardTiles.map((_, index) => index);
        const fallbackRowHeight = boardTiles.some(isDoubleTile) ? tileWidth : fallbackHeight;
        const fallbackWidth = boardTiles.reduce((width, piece, index) =>
          width + (index > 0 ? tileGap : 0) + (isDoubleTile(piece) ? fallbackHeight : tileWidth), 0);
        layout = {
          lines: [{ indices, left: 0, anchor: 0, width: fallbackWidth, height: fallbackRowHeight, direction: 1, top: 0 }],
          turns: [],
          height: fallbackRowHeight,
          tileHeight: fallbackHeight
        };
      }
      const tileHeight = layout?.tileHeight || tileWidth * 66 / 116;
      const horizontalDoubleIndices = new Set(layout.horizontalDoubleIndices || []);
      const pieceWidth = (tileIndex) =>
        isDoubleTile(boardTiles[tileIndex]) && !horizontalDoubleIndices.has(tileIndex) ? tileHeight : tileWidth;
      const openingCenter = (() => {
        for (const turn of layout.turns) {
          if (turn.index === openingIndex) {
            return { x: turn.left + tileHeight / 2, y: turn.top + tileWidth / 2 };
          }
        }

        for (const line of layout.lines) {
          if (!line.indices.includes(openingIndex)) continue;
          let cursor = line.direction > 0 ? line.left : line.left + line.width;
          for (const index of line.indices) {
            const width = pieceWidth(index);
            const left = line.direction > 0 ? cursor : cursor - width;
            if (line.direction > 0) cursor += width;
            else cursor = left;
            if (index === openingIndex) return { x: left + width / 2, y: line.top + line.height / 2 };
          }
        }

        return { x: boardWidth / 2, y: layout.height / 2 };
      })();
      let minX = Math.min(...layout.lines.map((line) => line.left));
      let maxX = Math.max(...layout.lines.map((line) => line.left + line.width));
      let minY = Math.min(...layout.lines.map((line) => line.top));
      let maxY = Math.max(...layout.lines.map((line) => line.top + line.height));
      for (const turn of layout.turns) {
        minX = Math.min(minX, turn.left);
        maxX = Math.max(maxX, turn.left + tileHeight);
        minY = Math.min(minY, turn.top);
        maxY = Math.max(maxY, turn.top + tileWidth);
      }
      const boardZoneWidth = boardZone?.clientWidth || window.innerWidth;
      const boardZoneHeight = boardZone?.clientHeight || window.innerHeight * 0.5;
      const baseLeft = (boardZoneWidth - boardWidth) / 2;
      const targetOffsetX = boardWidth / 2 - openingCenter.x;
      const minOffsetX = -baseLeft - minX;
      const maxOffsetX = boardZoneWidth - baseLeft - maxX;
      const offsetX = Math.max(minOffsetX, Math.min(maxOffsetX, targetOffsetX));
      const targetOffsetY = layout.height / 2 - openingCenter.y;
      const minOffsetY = layout.height / 2 - boardZoneHeight / 2 - minY;
      const maxOffsetY = layout.height / 2 + boardZoneHeight / 2 - maxY;
      const offsetY = Math.max(minOffsetY, Math.min(maxOffsetY, targetOffsetY));
      board.style.transform = `translate(calc(-50% + ${offsetX}px), calc(-50% + ${offsetY}px))`;
      board.style.setProperty("--board-tile-width", `${tileWidth}px`);
      board.style.setProperty("--board-tile-height", `${tileHeight}px`);
      board.style.height = `${layout?.height || tileHeight}px`;
      board.style.setProperty("--board-pip-core", `${Math.max(1.7, tileWidth * 0.036)}px`);
      board.style.setProperty("--board-pip-halo", `${Math.max(2.3, tileWidth * 0.045)}px`);

      for (let rowIndex = 0; rowIndex < layout.lines.length; rowIndex += 1) {
        const line = layout.lines[rowIndex];
        const row = document.createElement("div");
        const reverse = line.direction < 0;
        row.className = `board-row${reverse ? " reverse" : ""}`;
        row.style.position = "absolute";
        row.style.left = "0";
        row.style.top = `${line.top}px`;
        row.style.zIndex = String(rowIndex * 2 + 1);
        row.setAttribute("role", "group");
        row.setAttribute("aria-label", `Linha ${rowIndex + 1} da sequência`);
        if (reverse) row.style.paddingRight = `${boardWidth - line.anchor}px`;
        else row.style.paddingLeft = `${line.anchor}px`;

        for (const index of line.indices) {
          const piece = boardTiles[index];
          const firstValue = reverse ? piece.rightValue : piece.leftValue;
          const secondValue = reverse ? piece.leftValue : piece.rightValue;
          const isVerticalDouble = isDoubleTile(piece) && !horizontalDoubleIndices.has(index);
          const tile = createDomino(firstValue, secondValue, isVerticalDouble);
          const owner = piece.isBotTile ? (state.botName || "Adversário") : "Você";
          tile.setAttribute("aria-label", `${firstValue} e ${secondValue}, jogada por ${owner}`);
          tile.title = `Jogada de ${owner}`;
          tile.classList.add("board-tile");
          const badge = document.createElement("span");
          badge.className = `board-owner ${piece.isBotTile ? "bot" : "human"}`;
          badge.textContent = piece.isBotTile ? "OP" : "EU";
          badge.setAttribute("aria-hidden", "true");
          tile.append(badge);
          row.append(tile);
        }

        board.append(row);
      }

      for (let turnIndex = 0; turnIndex < layout.turns.length; turnIndex += 1) {
        const turn = layout.turns[turnIndex];
        const piece = boardTiles[turn.index];
        // PlacedTile values follow chain order: A connects from the row above, and B continues below.
        const tile = createDomino(piece.leftValue, piece.rightValue, true);
        const owner = piece.isBotTile ? (state.botName || "Adversário") : "Você";
        tile.setAttribute("aria-label", `${piece.leftValue} e ${piece.rightValue}, jogada por ${owner}; peça de virada`);
        tile.title = `Jogada de ${owner} · virada da sequência`;
        tile.classList.add("board-tile", "turn-connector");
        tile.style.left = `${turn.left}px`;
        tile.style.top = `${turn.top}px`;
        tile.style.zIndex = String(turnIndex * 2 + 2);
        const badge = document.createElement("span");
        badge.className = `board-owner ${piece.isBotTile ? "bot" : "human"}`;
        badge.textContent = piece.isBotTile ? "ADV" : "EU";
        badge.setAttribute("aria-hidden", "true");
        tile.append(badge);
        board.append(tile);
      }
    }

    function disableActions() {
      [leftButton, rightButton, buyButton, passButton].forEach((button) => { button.disabled = true; });
      hand.querySelectorAll("button").forEach((button) => { button.disabled = true; });
    }

    function setMessage(text) {
      message.textContent = text;
      message.hidden = !text;
    }

    function showScoreNotice(previous, updated) {
      if (!previous || !updated) return;
      const changes = [
        { name: updated.humanName || "Você", points: updated.humanScore - previous.humanScore },
        { name: updated.botName || "Adversário", points: updated.botScore - previous.botScore }
      ].filter(change => change.points > 0);
      if (changes.length === 0) return;

      const notice = $("#score-notice");
      $("#score-notice-player").textContent = changes.map(change => change.name).join(" · ");
      $("#score-notice-points").textContent = changes
        .map(change => `+${change.points} ${change.points === 1 ? "ponto" : "pontos"}`)
        .join(" · ");
      notice.hidden = false;
      notice.classList.remove("show");
      void notice.offsetWidth;
      notice.classList.add("show");
      window.clearTimeout(scoreNoticeTimer);
      scoreNoticeTimer = window.setTimeout(() => {
        notice.classList.remove("show");
        notice.hidden = true;
      }, 3600);
    }
  }

  function createDomino(valueA, valueB, isVertical, button = false) {
    const element = document.createElement(button ? "button" : "div");
    element.className = `domino ${isVertical ? "vertical" : "horizontal"}${button ? " hand-tile" : ""}`;
    if (button) element.type = "button";
    element.setAttribute("aria-label", `${valueA} e ${valueB}`);
    for (const value of [valueA, valueB]) {
      const half = document.createElement("span");
      half.className = "half";
      half.dataset.v = String(value);
      element.append(half);
    }
    return element;
  }

  function sameTile(first, second) {
    return (first[0] === second[0] && first[1] === second[1]) ||
      (first[0] === second[1] && first[1] === second[0]);
  }

  function tileText(tile) {
    return tile ? `${tile.valorA} | ${tile.valorB}` : "a pedra obrigatória";
  }

  function turnText(state) {
    if (state.phase === "Finished") return state.winnerName ? `${state.winnerName} venceu` : "Partida encerrada";
    if (state.isHumanTurn) {
      return ({ Opening: "Jogue a pedra de abertura", Play: "Sua vez", Buy: "Compre uma pedra", Pass: "Você deve passar" })[state.phase] || "Sua vez";
    }
    return state.turnPlayerName ? `Vez de ${state.turnPlayerName}` : "Aguardando a partida";
  }
})();
