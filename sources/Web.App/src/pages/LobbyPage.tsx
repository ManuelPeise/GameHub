import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import IconButton from "@mui/material/IconButton";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemText from "@mui/material/ListItemText";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import ContentCopy from "@mui/icons-material/ContentCopy";
import FlagOutlined from "@mui/icons-material/FlagOutlined";
import HistoryOutlined from "@mui/icons-material/HistoryOutlined";
import PauseCircleOutline from "@mui/icons-material/PauseCircleOutline";
import PersonOutline from "@mui/icons-material/PersonOutline";
import PlayArrow from "@mui/icons-material/PlayArrow";
import RemoveCircleOutline from "@mui/icons-material/RemoveCircleOutline";
import SmartToyOutlined from "@mui/icons-material/SmartToyOutlined";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import { useGameSessionEvents } from "../hooks/gameHub";
import {
  difficultyOptions,
  gameNames,
  sessionPagePaths,
  sessionUrls,
  type AddAiSeatRequest,
  type JoinSessionRequest,
  type SeatDto,
  type SessionDto,
  gamesWithAi,
} from "../api/sessions";
import ConnectionBanner from "../components/ConnectionBanner";
import Loading from "../components/Loading";
import Page from "../components/Page";
import SectionCard from "../components/SectionCard";
import StatusChip from "../components/StatusChip";
import KniffelGame from "../games/kniffel/KniffelGame";
import MuehleGame from "../games/muehle/MuehleGame";
import TicTacToeGame from "../games/tictactoe/TicTacToeGame";

type LobbyPageProps = {
  sessionId?: string;
  inviteCode?: string;
};

/** Fallback if the real-time connection is not available. */
const pollIntervalMs = 15000;

const connectionChip = (seat: SeatDto) => {
  if (seat.connectionStatus === "Disconnected") {
    return <Chip size="small" color="warning" label="Verbindung unterbrochen" />;
  }
  if (seat.connectionStatus === "Removed") {
    return <Chip size="small" label="entfernt" />;
  }
  return null;
};

/** Page of one session: lobby (players, invitation, AI seats, start) and the game board. */
const LobbyPage: React.FC<LobbyPageProps> = ({ sessionId, inviteCode }) => {
  const navigate = useNavigate();
  const [session, setSession] = React.useState<SessionDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [isStarting, setIsStarting] = React.useState(false);
  const [isChangingSeats, setIsChangingSeats] = React.useState(false);
  const [aiDifficulty, setAiDifficulty] = React.useState<string>(difficultyOptions[0].value);
  const [copied, setCopied] = React.useState<string | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    const run = async () => {
      try {
        if (inviteCode && !sessionId) {
          const joined = await sendRequest<SessionDto, JoinSessionRequest>("POST", {
            serviceUrl: sessionUrls.join,
            body: { inviteCode },
          });
          if (joined && !cancelled) {
            navigate(`${sessionPagePaths.lobbyPrefix}${joined.id}`, { replace: true });
          }
          return;
        }

        if (!sessionId) {
          setError("Die Partie wurde nicht gefunden.");
          return;
        }

        const current = await sendRequest<SessionDto>("GET", { serviceUrl: sessionUrls.byId(sessionId) });
        if (!cancelled) {
          setSession(current);
          setError(null);
        }
      } catch (failure) {
        if (!cancelled) {
          setError(getErrorMessage(failure, "Die Partie konnte nicht geladen werden."));
        }
      }
    };

    void run();
    const timer = inviteCode ? undefined : setInterval(() => void run(), pollIntervalMs);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [inviteCode, sessionId, navigate]);

  const reload = React.useCallback(async () => {
    if (!sessionId) {
      return;
    }
    try {
      setSession(await sendRequest<SessionDto>("GET", { serviceUrl: sessionUrls.byId(sessionId) }));
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Partie konnte nicht geladen werden."));
    }
  }, [sessionId]);

  useGameSessionEvents(sessionId, {
    onSessionUpdated: () => void reload(),
    onActionApplied: () => void reload(),
    onReconnected: () => void reload(),
  });

  const command = async (run: () => Promise<SessionDto | null>, fallback: string) => {
    setIsChangingSeats(true);
    setError(null);
    try {
      const updated = await run();
      if (updated) {
        setSession(updated);
      }
    } catch (failure) {
      setError(getErrorMessage(failure, fallback));
    } finally {
      setIsChangingSeats(false);
    }
  };

  const start = async () => {
    if (!session) {
      return;
    }
    setIsStarting(true);
    setError(null);
    try {
      setSession(await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.start(session.id) }));
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Partie konnte nicht gestartet werden."));
    } finally {
      setIsStarting(false);
    }
  };

  const copy = async (text: string, what: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(what);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      setCopied(null);
    }
  };

  if (!session) {
    return (
      <Page title={inviteCode ? "Beitreten" : "Partie"}>
        {error ? <Alert severity="error">{error}</Alert> : <Loading />}
      </Page>
    );
  }

  const seats = [...session.seats].sort((a, b) => a.order - b.order);
  const mySeat = seats.find((seat) => seat.id === session.mySeatId);
  const isLobby = session.status === "Lobby";
  const isOwner = isLobby && mySeat?.order === 0;
  const hasBoard = ["TicTacToe", "Kniffel", "Muehle"].includes(session.gameType) && !isLobby;
  const inviteLink = session.inviteCode ? `${window.location.origin}${sessionPagePaths.joinPrefix}${session.inviteCode}` : null;
  const onChanged = (updated?: SessionDto) => (updated ? setSession(updated) : void reload());
  const isSingleplayerOpen = session.mode === "Singleplayer" && (session.status === "Running" || session.status === "Paused");

  return (
    <Page
      title={gameNames[session.gameType] ?? session.gameType}
      subtitle={
        <Stack direction="row" spacing={1} component="span" sx={{ alignItems: "center", flexWrap: "wrap" }}>
          <span>{session.mode === "Singleplayer" ? "gegen die KI" : isLobby ? "Lobby" : "Mehrspieler"}</span>
          <StatusChip status={session.status} />
        </Stack>
      }
      width="lg"
      actions={
        (session.status === "Finished" || session.status === "Aborted") && !hasBoard ? (
          <Button href={`${sessionPagePaths.historyPrefix}${session.id}`} startIcon={<HistoryOutlined />}>
            Verlauf
          </Button>
        ) : undefined
      }
    >
      <ConnectionBanner />
      {error && <Alert severity="error">{error}</Alert>}

      {isLobby && session.inviteCode && (
        <SectionCard title="Mitspieler einladen">
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "center" } }}>
            <Typography sx={{ fontFamily: "monospace", fontSize: "2rem", letterSpacing: 6, textAlign: "center" }}>
              {session.inviteCode}
            </Typography>
            <Stack direction="row" spacing={1} sx={{ "& > *": { flex: { xs: 1, sm: "0 0 auto" } } }}>
              <Button variant="outlined" startIcon={<ContentCopy />} onClick={() => void copy(session.inviteCode!, "code")}>
                {copied === "code" ? "Kopiert" : "Code"}
              </Button>
              {inviteLink && (
                <Button variant="outlined" startIcon={<ContentCopy />} onClick={() => void copy(inviteLink, "link")}>
                  {copied === "link" ? "Kopiert" : "Link"}
                </Button>
              )}
            </Stack>
          </Stack>
          <Typography variant="body2" color="text.secondary">
            Die Lobby erscheint auch unter „Offene Partien“.
          </Typography>
        </SectionCard>
      )}

      {(isLobby || !hasBoard) && (
        <SectionCard title={`Spieler (${seats.length})`} icon={<PersonOutline color="primary" />}>
          <List disablePadding>
            {seats.map((seat) => (
              <ListItem
                key={seat.id}
                disableGutters
                secondaryAction={
                  isOwner &&
                  seat.kind === "Ai" && (
                    <Tooltip title="Entfernen">
                      <IconButton
                        edge="end"
                        aria-label={`${seat.displayName} entfernen`}
                        disabled={isChangingSeats}
                        onClick={() =>
                          void command(
                            () => sendRequest<SessionDto>("DELETE", { serviceUrl: sessionUrls.seat(session.id, seat.id) }),
                            "Der KI-Mitspieler konnte nicht entfernt werden.",
                          )
                        }
                      >
                        <RemoveCircleOutline />
                      </IconButton>
                    </Tooltip>
                  )
                }
              >
                <ListItemAvatar>
                  <Avatar sx={{ bgcolor: seat.kind === "Ai" ? "secondary.dark" : "primary.dark" }}>
                    {seat.kind === "Ai" ? <SmartToyOutlined /> : seat.displayName.slice(0, 1).toUpperCase()}
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={`${seat.displayName}${seat.id === session.mySeatId ? " (du)" : ""}`}
                  secondary={[seat.symbol, seat.order === 0 && isLobby ? "Ersteller" : null].filter(Boolean).join(" · ") || undefined}
                />
                {connectionChip(seat)}
              </ListItem>
            ))}
          </List>

          {isOwner && gamesWithAi.includes(session.gameType) && (
            <Stack direction="row" spacing={1}>
              <TextField
                select
                size="small"
                label="KI-Stufe"
                value={aiDifficulty}
                onChange={(event) => setAiDifficulty(event.target.value)}
                sx={{ maxWidth: 160 }}
              >
                {difficultyOptions.map((option) => (
                  <MenuItem key={option.value} value={option.value}>
                    {option.label}
                  </MenuItem>
                ))}
              </TextField>
              <Button
                variant="outlined"
                startIcon={<SmartToyOutlined />}
                disabled={isChangingSeats}
                onClick={() =>
                  void command(
                    () =>
                      sendRequest<SessionDto, AddAiSeatRequest>("POST", {
                        serviceUrl: sessionUrls.aiSeats(session.id),
                        body: { difficulty: aiDifficulty },
                      }),
                    "Der KI-Mitspieler konnte nicht hinzugefügt werden.",
                  )
                }
              >
                KI hinzufügen
              </Button>
            </Stack>
          )}

          {isOwner && (
            <Button variant="contained" size="large" startIcon={<PlayArrow />} loading={isStarting} onClick={() => void start()}>
              Partie starten
            </Button>
          )}
          {isLobby && !isOwner && (
            <Alert severity="info">Warte, bis der Ersteller die Partie startet.</Alert>
          )}
        </SectionCard>
      )}

      {hasBoard &&
        (session.gameType === "Kniffel" ? (
          <KniffelGame session={session} onChanged={onChanged} />
        ) : session.gameType === "Muehle" ? (
          <MuehleGame session={session} onChanged={onChanged} />
        ) : (
          <TicTacToeGame session={session} onChanged={onChanged} />
        ))}

      {isSingleplayerOpen && (
        <Stack direction="row" spacing={1} sx={{ justifyContent: "center", "& > *": { flex: { xs: 1, sm: "0 0 auto" } } }}>
          {session.status === "Running" && session.currentSeatId === session.mySeatId && (
            <Button
              variant="outlined"
              startIcon={<PauseCircleOutline />}
              disabled={isChangingSeats}
              onClick={() =>
                void command(
                  () => sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.pause(session.id) }),
                  "Die Partie konnte nicht pausiert werden.",
                )
              }
            >
              Pausieren
            </Button>
          )}
          {session.status === "Paused" && (
            <Button
              variant="contained"
              startIcon={<PlayArrow />}
              disabled={isChangingSeats}
              onClick={() =>
                void command(
                  () => sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.resume(session.id) }),
                  "Die Partie konnte nicht fortgesetzt werden.",
                )
              }
            >
              Fortsetzen
            </Button>
          )}
          <Button
            color="error"
            variant="outlined"
            startIcon={<FlagOutlined />}
            disabled={isChangingSeats}
            onClick={() =>
              void command(
                () => sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.abandon(session.id) }),
                "Die Partie konnte nicht aufgegeben werden.",
              )
            }
          >
            Aufgeben
          </Button>
        </Stack>
      )}
    </Page>
  );
};

export default LobbyPage;
