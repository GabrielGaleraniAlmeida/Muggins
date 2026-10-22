using System.Text.Json.Serialization;

namespace Muggins.Engine.Api;

/// <summary>DTOs matching the camelCase JSON contract in the API's OpenAPI document.</summary>
public sealed record SolicitarTokenRequestDto
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }
}

public sealed record SessaoResponseDto
{
    [JsonPropertyName("usuarioId")]
    public string? UsuarioId { get; init; }
}

public sealed record CriarJogadorRequestDto
{
    [JsonPropertyName("nome")]
    public string? Nome { get; init; }
}

public sealed record AtivarJogadorRequestDto
{
    [JsonPropertyName("chaveAtivacao")]
    public string? ChaveAtivacao { get; init; }
}

public sealed record PerfilJogadorDto
{
    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    [JsonPropertyName("ativo")]
    public bool Ativo { get; init; }

    [JsonPropertyName("ativadoEmUtc")]
    public DateTimeOffset? AtivadoEmUtc { get; init; }
}

public sealed record PartidaDisponivelDto
{
    [JsonPropertyName("partidaId")]
    public Guid PartidaId { get; init; }

    [JsonPropertyName("pontuacaoAlvo")]
    public int PontuacaoAlvo { get; init; }

    [JsonPropertyName("versaoEstado")]
    public long VersaoEstado { get; init; }
}

public sealed record CriarPartidaRequestDto
{
    [JsonPropertyName("pontuacaoAlvo")]
    public int PontuacaoAlvo { get; init; }

    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }

    [JsonPropertyName("webhook")]
    public string? Webhook { get; init; }
}

public sealed record IngressarRequestDto
{
    [JsonPropertyName("versaoEsperada")]
    public long VersaoEsperada { get; init; }

    [JsonPropertyName("chaveIdempotencia")]
    public Guid ChaveIdempotencia { get; init; }

    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }

    [JsonPropertyName("webhook")]
    public string? Webhook { get; init; }
}

public sealed record AcaoRequestDto
{
    [JsonPropertyName("versaoEsperada")]
    public long VersaoEsperada { get; init; }

    [JsonPropertyName("chaveIdempotencia")]
    public Guid ChaveIdempotencia { get; init; }

    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }
}

public sealed record JogarRequestDto
{
    [JsonPropertyName("valorA")]
    public int ValorA { get; init; }

    [JsonPropertyName("valorB")]
    public int ValorB { get; init; }

    [JsonPropertyName("lado")]
    public LadoJogavel Lado { get; init; }

    [JsonPropertyName("versaoEsperada")]
    public long VersaoEsperada { get; init; }

    [JsonPropertyName("chaveIdempotencia")]
    public Guid ChaveIdempotencia { get; init; }

    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }
}

public enum LadoJogavel
{
    Esquerda = 0,
    Direita = 1
}

public enum SituacaoPartida
{
    AguardandoInicio = 0,
    EmAndamento = 1,
    Encerrada = 2
}

/// <summary>Numeric values follow ProximaAcaoPartida in the API contract.</summary>
public enum ProximaAcaoPartida
{
    Aguardar = 0,
    JogarAbertura = 1,
    Jogar = 2,
    Comprar = 3,
    Passar = 4,
    Encerrada = 5
}

public sealed record PecaPartidaDto
{
    [JsonPropertyName("valorA")]
    public int ValorA { get; init; }

    [JsonPropertyName("valorB")]
    public int ValorB { get; init; }
}

public sealed record ResumoParticipantePartidaDto
{
    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    [JsonPropertyName("pontuacao")]
    public int Pontuacao { get; init; }

    [JsonPropertyName("quantidadePedrasNaMao")]
    public int QuantidadePedrasNaMao { get; init; }

    [JsonPropertyName("ehVez")]
    public bool EhVez { get; init; }
}

public sealed record EstadoPartidaParticipanteDto
{
    [JsonPropertyName("partidaId")]
    public Guid PartidaId { get; init; }

    [JsonPropertyName("versaoEstado")]
    public long VersaoEstado { get; init; }

    [JsonPropertyName("situacao")]
    public SituacaoPartida Situacao { get; init; }

    [JsonPropertyName("numeroRodada")]
    public int NumeroRodada { get; init; }

    [JsonPropertyName("jogadorDoTurnoId")]
    public Guid? JogadorDoTurnoId { get; init; }

    [JsonPropertyName("minhaMao")]
    public IReadOnlyList<PecaPartidaDto>? MinhaMao { get; init; }

    [JsonPropertyName("tabuleiro")]
    public IReadOnlyList<PecaPartidaDto>? Tabuleiro { get; init; }

    [JsonPropertyName("participantes")]
    public IReadOnlyList<ResumoParticipantePartidaDto>? Participantes { get; init; }

    [JsonPropertyName("vencedorId")]
    public Guid? VencedorId { get; init; }

    [JsonPropertyName("proximaAcao")]
    public ProximaAcaoPartida ProximaAcao { get; init; }

    [JsonPropertyName("pedraAberturaObrigatoria")]
    public PecaPartidaDto? PedraAberturaObrigatoria { get; init; }

    [JsonPropertyName("participacaoId")]
    public Guid ParticipacaoId { get; init; }
}

public sealed record EstadoPartidaObservadorDto
{
    [JsonPropertyName("partidaId")]
    public Guid PartidaId { get; init; }

    [JsonPropertyName("versaoEstado")]
    public long VersaoEstado { get; init; }

    [JsonPropertyName("situacao")]
    public SituacaoPartida Situacao { get; init; }

    [JsonPropertyName("numeroRodada")]
    public int NumeroRodada { get; init; }

    [JsonPropertyName("jogadorDoTurnoId")]
    public Guid? JogadorDoTurnoId { get; init; }

    [JsonPropertyName("tabuleiro")]
    public IReadOnlyList<PecaPartidaDto>? Tabuleiro { get; init; }

    [JsonPropertyName("participantes")]
    public IReadOnlyList<ResumoParticipantePartidaDto>? Participantes { get; init; }

    [JsonPropertyName("vencedorId")]
    public Guid? VencedorId { get; init; }
}

public sealed record ConfirmacaoAcaoPartidaDto
{
    [JsonPropertyName("chaveIdempotencia")]
    public Guid ChaveIdempotencia { get; init; }

    [JsonPropertyName("acaoIdempotente")]
    public bool AcaoIdempotente { get; init; }

    [JsonPropertyName("estado")]
    public EstadoPartidaParticipanteDto? Estado { get; init; }
}

public sealed record RespostaErroDto
{
    [JsonPropertyName("codigo")]
    public string? Codigo { get; init; }

    [JsonPropertyName("estadoAutoritativo")]
    public EstadoPartidaParticipanteDto? EstadoAutoritativo { get; init; }
}

public sealed record EventoHistoricoPartidaDto
{
    [JsonPropertyName("eventoId")]
    public Guid EventoId { get; init; }

    [JsonPropertyName("jogadorId")]
    public Guid JogadorId { get; init; }

    [JsonPropertyName("pecaJogada")]
    public PecaPartidaDto? PecaJogada { get; init; }

    [JsonPropertyName("lado")]
    public LadoJogavel? Lado { get; init; }

    [JsonPropertyName("passouVez")]
    public bool PassouVez { get; init; }

    [JsonPropertyName("pontosConcedidos")]
    public int PontosConcedidos { get; init; }

    [JsonPropertyName("recebidoEmUtc")]
    public DateTimeOffset RecebidoEmUtc { get; init; }
}

public sealed record ResultadoHistoricoPartidaDto
{
    [JsonPropertyName("partidaId")]
    public Guid PartidaId { get; init; }

    [JsonPropertyName("situacao")]
    public SituacaoPartida Situacao { get; init; }

    [JsonPropertyName("versaoEstado")]
    public long VersaoEstado { get; init; }

    [JsonPropertyName("vencedorId")]
    public Guid? VencedorId { get; init; }

    [JsonPropertyName("participantes")]
    public IReadOnlyList<ResumoParticipantePartidaDto>? Participantes { get; init; }

    [JsonPropertyName("eventos")]
    public IReadOnlyList<EventoHistoricoPartidaDto>? Eventos { get; init; }
}
