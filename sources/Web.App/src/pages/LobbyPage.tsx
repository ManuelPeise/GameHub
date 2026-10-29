import React from "react";
import { sendRequest } from "../hooks/apiClient";
import {
  sessionPagePaths,
  sessionUrls,
  type JoinSessionRequest,
  type SessionDto,
} from "../api/sessions";
import "./account/AccountPages.css";

type LobbyPageProps = {
  sessionId?: string;
  inviteCode?: string;
};

const pollIntervalMs = 2000;

const LobbyPage: React.FC<LobbyPageProps> = ({ sessionId, inviteCode }) => {
  const [session, setSession] = React.useState<SessionDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [isStarting, setIsStarting] = React.useState(false);

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

  const start = async () => {
    if (!session) {
      return;
    }

    setIsStarting(true);
    setError(null);
    try {
      const started = await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.start(session.id) });
      setSession(started);
    } catch {
      setError("Die Partie konnte nicht gestartet werden.");
    } finally {
      setIsStarting(false);
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
  const canStart = session.status === "Lobby" && mySeat?.order === 0;
  const inviteLink = session.inviteCode ? `${window.location.origin}${sessionPagePaths.joinPrefix}${session.inviteCode}` : null;

  return (
    <main className="account-page">
      <h1>Lobby</h1>

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
              </li>
            ))}
        </ul>
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
        <p className="account-success">Die Partie läuft. Die Spielansicht folgt in den nächsten Stories.</p>
      )}
    </main>
  );
};

export default LobbyPage;
