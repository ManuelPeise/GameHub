import React from "react";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import ConnectionBanner from "../components/ConnectionBanner";
import TurnTimer from "../components/TurnTimer";
import { useGameSessionEvents } from "../hooks/gameHub";
import {
  difficultyOptions,
  sessionPagePaths,
  sessionUrls,
  type AddAiSeatRequest,
  type JoinSessionRequest,
  type SessionDto,
} from "../api/sessions";
import "./account/AccountPages.css";

type LobbyPageProps = {
  sessionId?: string;
  inviteCode?: string;
};

/** Fallback if the real-time connection is not available. */
const pollIntervalMs = 15000;

const LobbyPage: React.FC<LobbyPageProps> = ({ sessionId, inviteCode }) => {
  const [session, setSession] = React.useState<SessionDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [isStarting, setIsStarting] = React.useState(false);
  const [isChangingSeats, setIsChangingSeats] = React.useState(false);
  const [aiDifficulty, setAiDifficulty] = React.useState<string>(difficultyOptions[0].value);

  React.useEffect(() => {
    let cancelled = false;
    const run = async () => {
      try {
        if (inviteCode && !sessionId) {
          const joined = await sendRequest<SessionDto, JoinSessionRequest>("POST", {
            serviceUrl: sessionUrls.join,
            body: { inviteCode },
          });
          if (!joined || cancelled) {
            return;
          }
          window.location.assign(`${sessionPagePaths.lobbyPrefix}${joined.id}`);
          return;
        }

        if (!sessionId) {
          setError("Die Lobby wurde nicht gefunden.");
          return;
        }

        const current = await sendRequest<SessionDto>("GET", { serviceUrl: sessionUrls.byId(sessionId) });
        if (cancelled) {
          return;
        }
        setSession(current);
        setError(null);
      } catch {
        if (!cancelled) {
          setError("Die Lobby konnte nicht geladen werden.");
        }
      }
    };

    void run();
    const timer = setInterval(() => {
      void run();
    }, pollIntervalMs);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [inviteCode, sessionId]);

  const reload = React.useCallback(async () => {
    if (!sessionId) {
      return;
    }
    try {
      setSession(await sendRequest<SessionDto>("GET", { serviceUrl: sessionUrls.byId(sessionId) }));
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Lobby konnte nicht geladen werden."));
    }
  }, [sessionId]);

  useGameSessionEvents(sessionId, {
    onSessionUpdated: () => void reload(),
    onActionApplied: () => void reload(),
    onReconnected: () => void reload(),
  });

  const start = async () => {
    if (!session) {
      return;
    }

    setIsStarting(true);
    setError(null);
    try {
      const started = await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.start(session.id) });
      setSession(started);
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Partie konnte nicht gestartet werden."));
    } finally {
      setIsStarting(false);
    }
  };

  const addAiSeat = async () => {
    if (!session) {
      return;
    }

    setIsChangingSeats(true);
    setError(null);
    try {
      const updated = await sendRequest<SessionDto, AddAiSeatRequest>("POST", {
        serviceUrl: sessionUrls.aiSeats(session.id),
        body: { difficulty: aiDifficulty },
      });
      setSession(updated);
    } catch (failure) {
      setError(getErrorMessage(failure, "Der KI-Mitspieler konnte nicht hinzugefügt werden."));
    } finally {
      setIsChangingSeats(false);
    }
  };

  const removeSeat = async (seatId: string) => {
    if (!session) {
      return;
    }

    setIsChangingSeats(true);
    setError(null);
    try {
      const updated = await sendRequest<SessionDto>("DELETE", { serviceUrl: sessionUrls.seat(session.id, seatId) });
      setSession(updated);
    } catch (failure) {
      setError(getErrorMessage(failure, "Der KI-Mitspieler konnte nicht entfernt werden."));
    } finally {
      setIsChangingSeats(false);
    }
  };

  if (!session) {
    return (
      <main className="account-page">
        <h1>Lobby</h1>
        <p>{error ?? "Lädt …"}</p>
      </main>
    );
  }

  const mySeat = session.seats.find((seat) => seat.id === session.mySeatId);
  const currentSeat = session.seats.find((seat) => seat.id === session.currentSeatId);
  const canStart = session.status === "Lobby" && mySeat?.order === 0;
  const inviteLink = session.inviteCode ? `${window.location.origin}${sessionPagePaths.joinPrefix}${session.inviteCode}` : null;

  return (
    <main className="account-page">
      <h1>Lobby</h1>
      <ConnectionBanner />

      {session.inviteCode && (
        <div className="account-form">
          <p>
            Einladungscode: <strong>{session.inviteCode}</strong>
          </p>
          {inviteLink && <p>Einladungslink: {inviteLink}</p>}
        </div>
      )}

      <section className="account-form">
        <h2>Teilnehmer</h2>
        <ul>
          {session.seats
            .slice()
            .sort((a, b) => a.order - b.order)
            .map((seat) => (
              <li key={seat.id}>
                {seat.displayName} ({seat.kind === "Ai" ? "KI" : "Mensch"})
                {seat.connectionStatus === "Disconnected" && " – Verbindung unterbrochen"}
                {seat.connectionStatus === "Removed" && " – aus der Partie entfernt"}
                {canStart && seat.kind === "Ai" && (
                  <button
                    type="button"
                    onClick={() => void removeSeat(seat.id)}
                    disabled={isChangingSeats}
                    aria-label={`${seat.displayName} entfernen`}
                  >
                    Entfernen
                  </button>
                )}
              </li>
            ))}
        </ul>
        {canStart && (
          <div className="account-field">
            <label htmlFor="ai-difficulty">KI-Mitspieler hinzufügen</label>
            <select id="ai-difficulty" value={aiDifficulty} onChange={(event) => setAiDifficulty(event.target.value)}>
              {difficultyOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
            <button type="button" onClick={() => void addAiSeat()} disabled={isChangingSeats}>
              KI hinzufügen
            </button>
          </div>
        )}
      </section>

      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}

      {canStart && (
        <button type="button" onClick={() => void start()} disabled={isStarting}>
          {isStarting ? "Wird gestartet …" : "Partie starten"}
        </button>
      )}

      {session.status === "Running" && (
        <p className="account-success">
          Die Partie läuft.
          {currentSeat && (
            <>
              {" "}
              Am Zug: {currentSeat.id === session.mySeatId ? "du" : currentSeat.displayName} <TurnTimer deadline={session.turnDeadline} />
            </>
          )}
        </p>
      )}

      {session.status === "Finished" && <p className="account-success">Die Partie ist beendet.</p>}
      {session.status === "Aborted" && <p className="account-error">Die Partie wurde abgebrochen.</p>}
    </main>
  );
};

export default LobbyPage;
