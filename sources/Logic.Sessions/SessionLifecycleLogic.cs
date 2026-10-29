using System.Security.Cryptography;
using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.Core.Randomness;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;
using Shared.Models.Interfaces;

namespace Logic.Sessions;

public enum SessionLifecycleFailureKind
{
    NotFound,
    Forbidden,
    Conflict,
    InvalidState,
    InvalidRequest,
}

public abstract record SessionLifecycleOutcome
{
    private SessionLifecycleOutcome()
    {
    }

    public sealed record Success(SessionSnapshot Session, Guid MySeatId) : SessionLifecycleOutcome;

    public sealed record Failure(SessionLifecycleFailureKind Kind, string Error) : SessionLifecycleOutcome;
}

public sealed class SessionLifecycleLogic(
    IGameSessionRepository repository,
    IUserAccountStore accounts,
    GameModuleRegistry modules,
    IAiStrategyRegistry strategies,
    IAiTurnScheduler scheduler,
    IAppSettings appSettings,
    TimeProvider timeProvider)
{
    private const int OpenLobbyListLimit = 50;

    private const int InviteCodeLength = 6;
    private const string InviteAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<SessionLifecycleOutcome> CreateAsync(Guid userId, CreateSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!modules.TryGet(request.GameType, out var module))
        {
            return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.InvalidRequest, "Dieses Spiel ist nicht verfügbar.");
        }

        var account = await accounts.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Forbidden, "Du bist nicht angemeldet.");
        }

        if (request.Mode == GameMode.Singleplayer)
        {
            if (string.IsNullOrWhiteSpace(request.Difficulty))
            {
                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.InvalidRequest,
                    "Bitte wähle für Singleplayer einen Schwierigkeitsgrad.");
            }

            var existing = await repository.FindOpenSingleplayerAsync(userId, request.GameType, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var existingSeat = existing.Seats.FirstOrDefault(seat => seat.UserId == userId);
                if (existingSeat is not null)
                {
                    return new SessionLifecycleOutcome.Success(existing, existingSeat.Id);
                }
            }

            var seatCount = module.MaxSeats;
            if (seatCount < 2)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.InvalidRequest, "Für dieses Spiel ist kein Singleplayer möglich.");
            }

            var preferredSeat = request.PreferredSeat ?? 0;
            if (preferredSeat < 0 || preferredSeat >= seatCount)
            {
                preferredSeat = 0;
            }

            var strategy = strategies.Get(request.GameType, request.Difficulty);
            var sessionId = Guid.NewGuid();
            var seed = CreateSeed();
            var state = module.CreateInitialState(seatCount, SeededRandom.ForAction(seed, 0));
            var progress = module.GetProgress(state);

            var seats = new List<SeatCreateWrite>(seatCount);
            for (var order = 0; order < seatCount; order++)
            {
                if (order == preferredSeat)
                {
                    seats.Add(new SeatCreateWrite(
                        Guid.NewGuid(),
                        order,
                        SeatKind.Human,
                        account.DisplayName,
                        Symbol(order),
                        userId,
                        null,
                        null,
                        SeatConnectionStatus.Connected,
                        null));
                }
                else
                {
                    seats.Add(new SeatCreateWrite(
                        Guid.NewGuid(),
                        order,
                        SeatKind.Ai,
                        $"KI ({request.Difficulty})",
                        Symbol(order),
                        null,
                        request.Difficulty,
                        strategy.Version,
                        SeatConnectionStatus.Connected,
                        null));
                }
            }

            var now = timeProvider.GetUtcNow();
            Guid? currentSeatId = progress.CurrentSeat is { } currentOrder ? seats.First(seat => seat.Order == currentOrder).Id : null;
            var created = await repository.CreateSessionAsync(
                    new SessionCreateWrite(
                        sessionId,
                        request.GameType,
                        GameMode.Singleplayer,
                        SessionStatus.Running,
                        userId,
                        null,
                        currentSeatId,
                        state,
                        0,
                        seed,
                        now,
                        seats),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!created)
            {
                existing = await repository.FindOpenSingleplayerAsync(userId, request.GameType, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    var existingSeat = existing.Seats.FirstOrDefault(seat => seat.UserId == userId);
                    if (existingSeat is not null)
                    {
                        return new SessionLifecycleOutcome.Success(existing, existingSeat.Id);
                    }
                }

                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.Conflict,
                    "Es existiert bereits eine offene Singleplayer-Partie für dieses Spiel.");
            }

            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.");
            }

            var mySeat = session.Seats.First(seat => seat.UserId == userId);
            if (session.CurrentSeatId is { } nextSeatId && session.Seats.Any(seat => seat.Id == nextSeatId && seat.Kind == SeatKind.Ai))
            {
                await scheduler.EnqueueAsync(session.Id, cancellationToken).ConfigureAwait(false);
            }

            return new SessionLifecycleOutcome.Success(session, mySeat.Id);
        }

        var ownerSeat = new SeatCreateWrite(
            Guid.NewGuid(),
            0,
            SeatKind.Human,
            account.DisplayName,
            Symbol(0),
            userId,
            null,
            null,
            SeatConnectionStatus.Connected,
            null);

        var inviteCode = await CreateUniqueInviteCodeAsync(cancellationToken).ConfigureAwait(false);
        if (inviteCode is null)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.Conflict,
                "Die Partie konnte nicht erstellt werden. Bitte versuche es erneut.");
        }

        var create = await repository.CreateSessionAsync(
                new SessionCreateWrite(
                    Guid.NewGuid(),
                    request.GameType,
                    GameMode.Multiplayer,
                    SessionStatus.Lobby,
                    userId,
                    inviteCode,
                    ownerSeat.Id,
                    "{}",
                    0,
                    CreateSeed(),
                    timeProvider.GetUtcNow(),
                    [ownerSeat]),
                cancellationToken)
            .ConfigureAwait(false);
        if (!create)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.Conflict,
                "Die Partie konnte nicht erstellt werden. Bitte versuche es erneut.");
        }

        var lobby = await repository.FindByInviteCodeAsync(inviteCode, cancellationToken).ConfigureAwait(false);
        return lobby is null
            ? new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.")
            : new SessionLifecycleOutcome.Success(lobby, ownerSeat.Id);
    }

    public async Task<SessionLifecycleOutcome> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.");
        }

        var seat = session.Seats.FirstOrDefault(candidate => candidate.UserId == userId);
        return seat is null
            ? new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Forbidden, "Du bist kein Teilnehmer dieser Partie.")
            : new SessionLifecycleOutcome.Success(session, seat.Id);
    }

    public async Task<SessionLifecycleOutcome> JoinAsync(Guid userId, JoinSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.InvalidRequest,
                "Bitte gib einen gültigen Einladungscode ein.");
        }

        var code = request.InviteCode.Trim().ToUpperInvariant();
        return await JoinCoreAsync(
                userId,
                ct => repository.FindByInviteCodeAsync(code, ct),
                "Der Einladungscode ist ungültig.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Join from the public list of open lobbies.</summary>
    public Task<SessionLifecycleOutcome> JoinByIdAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        JoinCoreAsync(
            userId,
            ct => repository.GetAsync(sessionId, ct),
            "Die Partie wurde nicht gefunden.",
            cancellationToken);

    /// <summary>Open multiplayer lobbies that still have a free seat (public list).</summary>
    public async Task<IReadOnlyList<OpenLobbyDto>> ListOpenLobbiesAsync(Guid userId, string? gameType, CancellationToken cancellationToken)
    {
        var lobbies = await repository.GetOpenLobbiesAsync(gameType, OpenLobbyListLimit, cancellationToken).ConfigureAwait(false);
        return
        [
            .. lobbies
                .Where(lobby => modules.TryGet(lobby.GameType, out _))
                .Select(lobby => (Lobby: lobby, MaxSeats: modules.Get(lobby.GameType).MaxSeats))
                .Where(entry => entry.Lobby.SeatCount < entry.MaxSeats)
                .Select(entry => new OpenLobbyDto(
                    entry.Lobby.Id,
                    entry.Lobby.GameType,
                    entry.Lobby.OwnerName,
                    entry.Lobby.SeatCount,
                    entry.MaxSeats,
                    entry.Lobby.OwnerUserId == userId,
                    entry.Lobby.CreatedAt)),
        ];
    }

    /// <summary>The owner adds an AI seat to the lobby (if allowed by App:Game:AllowAiInMultiplayer).</summary>
    public async Task<SessionLifecycleOutcome> AddAiSeatAsync(Guid userId, Guid sessionId, AddAiSeatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!appSettings.Game.AllowAiInMultiplayer)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.InvalidRequest,
                "KI-Mitspieler sind im Multiplayer nicht erlaubt.");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var (session, ownerSeat, failure) = await LoadOwnLobbyAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            IAiPlayer strategy;
            try
            {
                strategy = strategies.Get(session!.GameType, request.Difficulty);
            }
            catch (InvalidOperationException)
            {
                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.InvalidRequest,
                    "Diesen Schwierigkeitsgrad gibt es für dieses Spiel nicht.");
            }

            if (session.Seats.Count >= modules.Get(session.GameType).MaxSeats)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Conflict, "Die Lobby ist bereits voll.");
            }

            var order = session.Seats.Count;
            var seat = new SeatCreateWrite(
                Guid.NewGuid(),
                order,
                SeatKind.Ai,
                $"KI ({strategy.Difficulty})",
                Symbol(order),
                null,
                strategy.Difficulty,
                strategy.Version,
                SeatConnectionStatus.Connected,
                null);

            if (await repository.AddSeatAsync(new SeatAddWrite(session.Id, session.StateVersion, seat, timeProvider.GetUtcNow()), cancellationToken)
                .ConfigureAwait(false))
            {
                var updated = await repository.GetAsync(session.Id, cancellationToken).ConfigureAwait(false);
                return updated is null
                    ? new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.")
                    : new SessionLifecycleOutcome.Success(updated, ownerSeat!.Id);
            }
        }

        return new SessionLifecycleOutcome.Failure(
            SessionLifecycleFailureKind.Conflict,
            "Die Lobby wurde parallel geändert. Bitte versuche es erneut.");
    }

    /// <summary>The owner removes an AI seat from the lobby; the following seats move up.</summary>
    public async Task<SessionLifecycleOutcome> RemoveAiSeatAsync(Guid userId, Guid sessionId, Guid seatId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var (session, ownerSeat, failure) = await LoadOwnLobbyAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var seat = session!.Seats.FirstOrDefault(candidate => candidate.Id == seatId);
            if (seat is null || seat.Kind != SeatKind.Ai)
            {
                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.InvalidRequest,
                    "Nur KI-Mitspieler können entfernt werden.");
            }

            var reorders = session.Seats
                .Where(candidate => candidate.Order > seat.Order)
                .OrderBy(candidate => candidate.Order)
                .Select(candidate => new SeatReorder(candidate.Id, candidate.Order - 1, Symbol(candidate.Order - 1)))
                .ToList();

            if (await repository.RemoveSeatAsync(
                    new SeatRemoveWrite(session.Id, session.StateVersion, seat.Id, reorders, timeProvider.GetUtcNow()),
                    cancellationToken)
                .ConfigureAwait(false))
            {
                var updated = await repository.GetAsync(session.Id, cancellationToken).ConfigureAwait(false);
                return updated is null
                    ? new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.")
                    : new SessionLifecycleOutcome.Success(updated, ownerSeat!.Id);
            }
        }

        return new SessionLifecycleOutcome.Failure(
            SessionLifecycleFailureKind.Conflict,
            "Die Lobby wurde parallel geändert. Bitte versuche es erneut.");
    }

    private async Task<(SessionSnapshot? Session, SeatSnapshot? OwnerSeat, SessionLifecycleOutcome.Failure? Failure)> LoadOwnLobbyAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return (null, null, new(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden."));
        }

        if (session.OwnerUserId != userId)
        {
            return (null, null, new(SessionLifecycleFailureKind.Forbidden, "Nur der Ersteller kann die Lobby ändern."));
        }

        if (session.Status != SessionStatus.Lobby)
        {
            return (null, null, new(SessionLifecycleFailureKind.InvalidState, "Die Partie befindet sich nicht mehr in der Lobby."));
        }

        var ownerSeat = session.Seats.First(seat => seat.UserId == userId);
        return (session, ownerSeat, null);
    }

    private async Task<SessionLifecycleOutcome> JoinCoreAsync(
        Guid userId,
        Func<CancellationToken, Task<SessionSnapshot?>> loadSession,
        string notFoundError,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Forbidden, "Du bist nicht angemeldet.");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var session = await loadSession(cancellationToken).ConfigureAwait(false);
            if (session is null || session.Mode != GameMode.Multiplayer)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, notFoundError);
            }

            if (session.Status != SessionStatus.Lobby)
            {
                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.InvalidState,
                    "Die Partie befindet sich nicht mehr in der Lobby.");
            }

            var existingSeat = session.Seats.FirstOrDefault(seat => seat.UserId == userId);
            if (existingSeat is not null)
            {
                return new SessionLifecycleOutcome.Success(session, existingSeat.Id);
            }

            var module = modules.Get(session.GameType);
            if (session.Seats.Count >= module.MaxSeats)
            {
                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.Conflict,
                    "Die Lobby ist bereits voll.");
            }

            var seat = new SeatCreateWrite(
                Guid.NewGuid(),
                session.Seats.Count,
                SeatKind.Human,
                account.DisplayName,
                Symbol(session.Seats.Count),
                userId,
                null,
                null,
                SeatConnectionStatus.Connected,
                null);

            var added = await repository.AddSeatAsync(
                    new SeatAddWrite(session.Id, session.StateVersion, seat, timeProvider.GetUtcNow()),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!added)
            {
                continue;
            }

            var updated = await repository.GetAsync(session.Id, cancellationToken).ConfigureAwait(false);
            return updated is null
                ? new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.")
                : new SessionLifecycleOutcome.Success(updated, seat.Id);
        }

        return new SessionLifecycleOutcome.Failure(
            SessionLifecycleFailureKind.Conflict,
            "Die Lobby wurde parallel geändert. Bitte versuche es erneut.");
    }

    public async Task<SessionLifecycleOutcome> StartAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.");
            }

            var mySeat = session.Seats.FirstOrDefault(seat => seat.UserId == userId);
            if (mySeat is null)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Forbidden, "Du bist kein Teilnehmer dieser Partie.");
            }

            if (session.OwnerUserId != userId)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Forbidden, "Nur der Ersteller kann die Partie starten.");
            }

            if (session.Status != SessionStatus.Lobby)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.InvalidState, "Die Partie läuft bereits.");
            }

            var module = modules.Get(session.GameType);
            if (session.Seats.Count < module.MinSeats)
            {
                return new SessionLifecycleOutcome.Failure(
                    SessionLifecycleFailureKind.InvalidState,
                    "Es sind noch nicht genug Spieler in der Lobby.");
            }

            var state = module.CreateInitialState(session.Seats.Count, SeededRandom.ForAction(session.RngSeed, 0));
            var progress = module.GetProgress(state);
            var currentSeatId = progress.CurrentSeat is { } currentSeat
                ? session.Seats.FirstOrDefault(seat => seat.Order == currentSeat)?.Id
                : null;

            var started = await repository.StartSessionAsync(
                    new SessionStartWrite(session.Id, session.StateVersion, currentSeatId, state, timeProvider.GetUtcNow()),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!started)
            {
                continue;
            }

            var updated = await repository.GetAsync(session.Id, cancellationToken).ConfigureAwait(false);
            if (updated is null)
            {
                return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.");
            }

            if (updated.CurrentSeatId is { } nextSeatId && updated.Seats.Any(seat => seat.Id == nextSeatId && seat.Kind == SeatKind.Ai))
            {
                await scheduler.EnqueueAsync(updated.Id, cancellationToken).ConfigureAwait(false);
            }

            return new SessionLifecycleOutcome.Success(updated, mySeat.Id);
        }

        return new SessionLifecycleOutcome.Failure(
            SessionLifecycleFailureKind.Conflict,
            "Die Lobby wurde parallel geändert. Bitte versuche es erneut.");
    }

    public async Task<SessionLifecycleOutcome> RematchAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var previous = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.");
        }

        var mySeat = previous.Seats.FirstOrDefault(seat => seat.UserId == userId);
        if (mySeat is null)
        {
            return new SessionLifecycleOutcome.Failure(SessionLifecycleFailureKind.Forbidden, "Du bist kein Teilnehmer dieser Partie.");
        }

        if (previous.Status == SessionStatus.Lobby)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.InvalidState,
                "Eine Lobby kann nicht als Rematch kopiert werden.");
        }

        var module = modules.Get(previous.GameType);
        var seats = previous.Seats
            .OrderBy(seat => seat.Order)
            .Select(seat => new SeatCreateWrite(
                Guid.NewGuid(),
                seat.Order,
                seat.Kind,
                seat.DisplayName,
                seat.Symbol,
                seat.UserId,
                seat.AiDifficulty,
                seat.AiStrategyVersion,
                SeatConnectionStatus.Connected,
                null))
            .ToList();

        var seed = CreateSeed();
        var state = module.CreateInitialState(seats.Count, SeededRandom.ForAction(seed, 0));
        var progress = module.GetProgress(state);
        Guid? currentSeatId = progress.CurrentSeat is { } currentOrder ? seats.First(seat => seat.Order == currentOrder).Id : null;
        var now = timeProvider.GetUtcNow();
        var rematchId = Guid.NewGuid();

        var created = await repository.CreateSessionAsync(
                new SessionCreateWrite(
                    rematchId,
                    previous.GameType,
                    previous.Mode,
                    SessionStatus.Running,
                    previous.OwnerUserId,
                    null,
                    currentSeatId,
                    state,
                    0,
                    seed,
                    now,
                    seats),
                cancellationToken)
            .ConfigureAwait(false);
        if (!created)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.Conflict,
                "Die neue Partie konnte nicht erstellt werden. Bitte versuche es erneut.");
        }

        var rematch = await repository.GetAsync(rematchId, cancellationToken).ConfigureAwait(false);
        if (rematch is null)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.NotFound,
                "Die neue Partie wurde nicht gefunden.");
        }

        var mappedSeat = rematch.Seats.FirstOrDefault(seat => seat.UserId == userId);
        if (mappedSeat is null)
        {
            return new SessionLifecycleOutcome.Failure(
                SessionLifecycleFailureKind.NotFound,
                "Die neue Partie enthält deinen Sitz nicht.");
        }

        if (rematch.CurrentSeatId is { } nextSeatId && rematch.Seats.Any(seat => seat.Id == nextSeatId && seat.Kind == SeatKind.Ai))
        {
            await scheduler.EnqueueAsync(rematch.Id, cancellationToken).ConfigureAwait(false);
        }

        return new SessionLifecycleOutcome.Success(rematch, mappedSeat.Id);
    }

    private async Task<string?> CreateUniqueInviteCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = GenerateInviteCode();
            var existing = await repository.FindByInviteCodeAsync(code, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return code;
            }
        }

        return null;
    }

    private static string GenerateInviteCode()
    {
        Span<char> chars = stackalloc char[InviteCodeLength];
        Span<byte> bytes = stackalloc byte[InviteCodeLength];
        RandomNumberGenerator.Fill(bytes);

        for (var i = 0; i < InviteCodeLength; i++)
        {
            chars[i] = InviteAlphabet[bytes[i] % InviteAlphabet.Length];
        }

        return new string(chars);
    }

    private static long CreateSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToInt64(bytes);
    }

    private static string? Symbol(int order) => order switch
    {
        0 => "X",
        1 => "O",
        _ => null,
    };
}
