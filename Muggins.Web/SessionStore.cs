using System.Security.Cryptography;
using System.Text.Json;
using Muggins.Engine.Domain;
using Microsoft.AspNetCore.DataProtection;

namespace Muggins.Web;

public sealed class BrowserSessionData
{
    public string? AccessToken { get; set; }
    public string? UserId { get; set; }
    public Guid? HumanPlayerId { get; set; }
    public Guid? BotPlayerId { get; set; }
    public Dictionary<Guid, string> ActivationKeys { get; set; } = [];
    public Guid? MatchId { get; set; }
    public int TargetScore { get; set; } = 100;
    public int Round { get; set; }
    public int? LeftEnd { get; set; }
    public int? RightEnd { get; set; }
    public int? OpeningTileIndex { get; set; }
    public List<PlacedTile> Board { get; set; } = [];
    public PendingApiAction? PendingAction { get; set; }

    public void ResetMatch()
    {
        MatchId = null;
        Round = 0;
        LeftEnd = null;
        RightEnd = null;
        OpeningTileIndex = null;
        Board.Clear();
        PendingAction = null;
    }
}

public sealed record PlacedTile(int LeftValue, int RightValue, Guid PlayerId);

public sealed record PendingApiAction(
    string Role,
    string Kind,
    int? ValueA,
    int? ValueB,
    MoveSide? Side,
    long ExpectedVersion,
    Guid IdempotencyKey);

public sealed class EncryptedSessionStore
{
    private const string CookieName = "muggins.sid";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _sessionDirectory;
    private readonly IDataProtector _protector;
    private readonly Dictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly object _locksGate = new();

    public EncryptedSessionStore(string dataDirectory, IDataProtectionProvider protectionProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(protectionProvider);
        _sessionDirectory = Path.Combine(dataDirectory, "sessions");
        Directory.CreateDirectory(_sessionDirectory);
        _protector = protectionProvider.CreateProtector("Muggins.Web.BrowserSession.v1");
        RestrictDirectoryPermissions(_sessionDirectory);
    }

    public async Task<SessionLease> OpenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Cookies[CookieName];
        if (!IsValidSessionId(sessionId))
        {
            sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            context.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Path = "/",
                MaxAge = TimeSpan.FromDays(90)
            });
        }

        var gate = GetLock(sessionId!);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(sessionId!, cancellationToken);
            return new SessionLease(this, sessionId!, gate, data);
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    private SemaphoreSlim GetLock(string id)
    {
        lock (_locksGate)
        {
            if (!_locks.TryGetValue(id, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _locks.Add(id, gate);
            }

            return gate;
        }
    }

    private async Task<BrowserSessionData> ReadAsync(string id, CancellationToken cancellationToken)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
        {
            return new BrowserSessionData();
        }

        var protectedText = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            var json = _protector.Unprotect(protectedText);
            return JsonSerializer.Deserialize<BrowserSessionData>(json, JsonOptions) ?? new BrowserSessionData();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            throw new InvalidDataException("Não foi possível ler a sessão local protegida.", exception);
        }
    }

    private async Task WriteAsync(string id, BrowserSessionData data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var protectedText = _protector.Protect(json);
        var path = GetPath(id);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, protectedText, cancellationToken);
            RestrictFilePermissions(tempPath);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private string GetPath(string id) => Path.Combine(_sessionDirectory, id + ".bin");

    private static bool IsValidSessionId(string? id) =>
        id is { Length: 64 } && id.All(Uri.IsHexDigit);

    private static void RestrictDirectoryPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (PlatformNotSupportedException)
        {
            // The operating system will apply its normal private-data defaults.
        }
    }

    private static void RestrictFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
            // The operating system will apply its normal private-data defaults.
        }
    }

    public sealed class SessionLease : IAsyncDisposable
    {
        private readonly EncryptedSessionStore _owner;
        private readonly string _sessionId;
        private readonly SemaphoreSlim _gate;
        private bool _disposed;

        internal SessionLease(
            EncryptedSessionStore owner,
            string sessionId,
            SemaphoreSlim gate,
            BrowserSessionData data)
        {
            _owner = owner;
            _sessionId = sessionId;
            _gate = gate;
            Data = data;
        }

        public BrowserSessionData Data { get; }

        public Task SaveAsync(CancellationToken cancellationToken = default) =>
            _owner.WriteAsync(_sessionId, Data, cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
