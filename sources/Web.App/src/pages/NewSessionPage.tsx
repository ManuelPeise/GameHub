import React from "react";
import { sendRequest } from "../hooks/apiClient";
import { pagePaths } from "../api/account";
import {
  sessionPagePaths,
  sessionUrls,
  type CreateSessionRequest,
  type SessionDto,
} from "../api/sessions";
import "./account/AccountPages.css";

const NewSessionPage: React.FC = () => {
  const [mode, setMode] = React.useState<"Singleplayer" | "Multiplayer">("Singleplayer");
  const [difficulty, setDifficulty] = React.useState("Easy");
  const [message, setMessage] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setIsSubmitting(true);

    const request: CreateSessionRequest = {
      gameType: "TicTacToe",
      mode,
      difficulty: mode === "Singleplayer" ? difficulty : null,
      preferredSeat: 0,
    };

    try {
      const created = await sendRequest<SessionDto, CreateSessionRequest>("POST", {
        serviceUrl: sessionUrls.base,
        body: request,
      });
      if (!created) {
        setError("Die Partie konnte nicht erstellt werden.");
        return;
      }

      if (created.mode === "Multiplayer") {
        window.location.assign(`${sessionPagePaths.lobbyPrefix}${created.id}`);
        return;
      }

      setMessage("Singleplayer-Partie erstellt. Die Spielansicht folgt mit den nächsten Stories.");
    } catch {
      setError("Die Partie konnte nicht erstellt werden. Bitte versuche es erneut.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="account-page">
      <h1>Neue Partie</h1>
      <form className="account-form" onSubmit={submit}>
        <div className="account-field">
          <label htmlFor="mode">Modus</label>
          <select
            id="mode"
            value={mode}
            onChange={(event) => setMode(event.target.value as "Singleplayer" | "Multiplayer")}
          >
            <option value="Singleplayer">Singleplayer</option>
            <option value="Multiplayer">Multiplayer</option>
          </select>
        </div>

        {mode === "Singleplayer" && (
          <div className="account-field">
            <label htmlFor="difficulty">Schwierigkeitsgrad</label>
            <select id="difficulty" value={difficulty} onChange={(event) => setDifficulty(event.target.value)}>
              <option value="Easy">Einfach</option>
              <option value="Medium">Mittel</option>
              <option value="Hard">Schwer</option>
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
