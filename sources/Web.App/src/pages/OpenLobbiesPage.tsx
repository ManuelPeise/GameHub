import React from "react";
import { pagePaths } from "../api/account";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import {
  gameNames,
  sessionPagePaths,
  sessionUrls,
  type JoinSessionRequest,
  type OpenLobbyDto,
  type SessionDto,
} from "../api/sessions";
import "./account/AccountPages.css";

const pollIntervalMs = 5000;

/** Public list of open multiplayer lobbies; join by click or by invite code. */
const OpenLobbiesPage: React.FC = () => {
  const [lobbies, setLobbies] = React.useState<OpenLobbyDto[] | null>(null);
  const [inviteCode, setInviteCode] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [isJoining, setIsJoining] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const open = await sendRequest<OpenLobbyDto[]>("GET", { serviceUrl: sessionUrls.open });
        if (!cancelled) {
          setLobbies(open ?? []);
        }
      } catch (failure) {
        if (!cancelled) {
          setError(getErrorMessage(failure, "Die offenen Partien konnten nicht geladen werden."));
        }
      }
    };

    void load();
    const timer = setInterval(() => void load(), pollIntervalMs);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, []);

  const goToLobby = (session: SessionDto | null) => {
    if (session) {
      window.location.assign(`${sessionPagePaths.lobbyPrefix}${session.id}`);
    }
  };

  const joinById = async (lobby: OpenLobbyDto) => {
    if (lobby.isMine) {
      window.location.assign(`${sessionPagePaths.lobbyPrefix}${lobby.id}`);
      return;
    }

    setIsJoining(true);
    setError(null);
    try {
      goToLobby(await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.joinById(lobby.id) }));
    } catch (failure) {
      setError(getErrorMessage(failure, "Beitritt fehlgeschlagen."));
    } finally {
      setIsJoining(false);
    }
  };

  const joinByCode = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsJoining(true);
    setError(null);
    try {
      goToLobby(
        await sendRequest<SessionDto, JoinSessionRequest>("POST", {
          serviceUrl: sessionUrls.join,
          body: { inviteCode: inviteCode.trim() },
        }),
      );
    } catch (failure) {
      setError(getErrorMessage(failure, "Beitritt fehlgeschlagen."));
    } finally {
      setIsJoining(false);
    }
  };

  return (
    <main className="account-page">
      <h1>Offene Partien</h1>

      <section className="account-form">
        {lobbies === null && <p>Lädt …</p>}
        {lobbies?.length === 0 && <p>Zurzeit gibt es keine offenen Partien.</p>}
        {lobbies && lobbies.length > 0 && (
          <ul>
            {lobbies.map((lobby) => (
              <li key={lobby.id}>
                {gameNames[lobby.gameType] ?? lobby.gameType} von {lobby.ownerName} – {lobby.seatCount}/{lobby.maxSeats} Spieler{" "}
                <button type="button" onClick={() => void joinById(lobby)} disabled={isJoining}>
                  {lobby.isMine ? "Öffnen" : "Beitreten"}
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <form className="account-form" onSubmit={joinByCode}>
        <div className="account-field">
          <label htmlFor="invite-code">Einladungscode</label>
          <input
            id="invite-code"
            value={inviteCode}
            onChange={(event) => setInviteCode(event.target.value)}
            autoComplete="off"
            required
          />
        </div>
        <button type="submit" disabled={isJoining || inviteCode.trim().length === 0}>
          Mit Code beitreten
        </button>
      </form>

      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}

      <p className="account-links">
        <a href={sessionPagePaths.newSession}>Neue Partie</a>
        <a href={pagePaths.home}>Zur Startseite</a>
      </p>
    </main>
  );
};

export default OpenLobbiesPage;
