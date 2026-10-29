import React from "react";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import { pagePaths } from "../api/account";
import {
  gameNames,
  sessionPagePaths,
  sessionUrls,
  statusLabels,
  type CreateSessionRequest,
  type SessionDto,
} from "../api/sessions";
import "./account/AccountPages.css";

const NewSessionPage: React.FC = () => {
  const [mode, setMode] = React.useState<"Singleplayer" | "Multiplayer">(
    "Singleplayer",
  );
  const [difficulty, setDifficulty] = React.useState("Easy");
  const [preferredSeat, setPreferredSeat] = React.useState(0);
  const [gameType, setGameType] = React.useState<"TicTacToe" | "Kniffel">("TicTacToe");
  const [message, setMessage] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [openSessions, setOpenSessions] = React.useState<SessionDto[]>([]);

  const loadOpen = React.useCallback(async () => {
    try {
      setOpenSessions(
        (await sendRequest<SessionDto[]>("GET", {
          serviceUrl: sessionUrls.mine,
        })) ?? [],
      );
    } catch {
      setOpenSessions([]);
    }
  }, []);

  React.useEffect(() => {
    const onLoad = async () => {
      await loadOpen();
    };
    void onLoad();
  }, [loadOpen]);

  const abandon = async (session: SessionDto) => {
    setError(null);
    try {
      await sendRequest<SessionDto>("POST", {
        serviceUrl: sessionUrls.abandon(session.id),
      });
      await loadOpen();
    } catch (failure) {
      setError(
        getErrorMessage(failure, "Die Partie konnte nicht aufgegeben werden."),
      );
    }
  };

  const openSingleplayer = openSessions.find(
    (session) =>
      session.mode === "Singleplayer" && session.gameType === gameType,
  );

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setIsSubmitting(true);

    const request: CreateSessionRequest = {
      gameType,
      mode,
      difficulty: mode === "Singleplayer" ? difficulty : null,
      preferredSeat: mode === "Singleplayer" && gameType === "TicTacToe" ? preferredSeat : 0,
    };

    try {
      const created = await sendRequest<SessionDto, CreateSessionRequest>(
        "POST",
        {
          serviceUrl: sessionUrls.base,
          body: request,
        },
      );
      if (!created) {
        setError("Die Partie konnte nicht erstellt werden.");
        return;
      }

      window.location.assign(`${sessionPagePaths.lobbyPrefix}${created.id}`);
    } catch {
      setError(
        "Die Partie konnte nicht erstellt werden. Bitte versuche es erneut.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="account-page">
      <h1>Neue Partie</h1>

      {openSessions.length > 0 && (
        <section className="account-form">
          <h2>Deine offenen Partien</h2>
          <ul>
            {openSessions.map((session) => (
              <li key={session.id}>
                {gameNames[session.gameType] ?? session.gameType} (
                {session.mode === "Singleplayer" ? "gegen KI" : "Multiplayer"},{" "}
                {statusLabels[session.status]}){" "}
                <a href={`${sessionPagePaths.lobbyPrefix}${session.id}`}>
                  {session.status === "Paused" ? "Fortsetzen" : "Öffnen"}
                </a>{" "}
                {session.mode === "Singleplayer" && (
                  <button type="button" onClick={() => void abandon(session)}>
                    Aufgeben
                  </button>
                )}
              </li>
            ))}
          </ul>
          {mode === "Singleplayer" && openSingleplayer && (
            <p className="account-error" role="status">
              Du hast bereits eine offene Partie gegen die KI. „Partie
              erstellen“ öffnet sie; gib sie auf, um neu zu beginnen.
            </p>
          )}
        </section>
      )}
      <form className="account-form" onSubmit={submit}>
        <div className="account-field">
          <label htmlFor="game">Spiel</label>
          <select
            id="game"
            value={gameType}
            onChange={(event) => setGameType(event.target.value as "TicTacToe" | "Kniffel")}
          >
            <option value="TicTacToe">Tic-Tac-Toe</option>
            <option value="Kniffel">Kniffel (2–4 Spieler)</option>
          </select>
        </div>

        <div className="account-field">
          <label htmlFor="mode">Modus</label>
          <select
            id="mode"
            value={mode}
            onChange={(event) =>
              setMode(event.target.value as "Singleplayer" | "Multiplayer")
            }
          >
            <option value="Singleplayer">Singleplayer</option>
            <option value="Multiplayer">Multiplayer</option>
          </select>
        </div>

        {mode === "Singleplayer" && (
          <div className="account-field">
            <label htmlFor="difficulty">Schwierigkeitsgrad</label>
            <select
              id="difficulty"
              value={difficulty}
              onChange={(event) => setDifficulty(event.target.value)}
            >
              <option value="Easy">Einfach</option>
              <option value="Medium">Mittel</option>
              <option value="Hard">Schwer</option>
            </select>
          </div>
        )}

        {mode === "Singleplayer" && gameType === "TicTacToe" && (
          <div className="account-field">
            <label htmlFor="symbol">Du spielst</label>
            <select
              id="symbol"
              value={preferredSeat}
              onChange={(event) => setPreferredSeat(Number(event.target.value))}
            >
              <option value={0}>X (du beginnst)</option>
              <option value={1}>O (die KI beginnt)</option>
            </select>
          </div>
        )}

        {error && (
          <p className="account-error" role="alert">
            {error}
          </p>
        )}

        {message && (
          <p className="account-success" role="status">
            {message}
          </p>
        )}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Wird erstellt …" : "Partie erstellen"}
        </button>
      </form>

      <p className="account-links">
        <a href={pagePaths.home}>Zur Startseite</a>
      </p>
    </main>
  );
};

export default NewSessionPage;
