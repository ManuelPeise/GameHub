import React from "react";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { ApiError, getErrorMessage, sendRequest } from "../../hooks/apiClient";
import { useGameSessionEvents } from "../../hooks/gameHub";
import { sessionUrls, type SessionDto, type SubmitSessionActionRequest } from "../../api/sessions";
import { GameEndActions, GameStatusBar } from "../GameParts";
import MuehleBoard from "./MuehleBoard";
import {
  applyStep,
  colorName,
  colorOf,
  emptyPoint,
  endReasonLabels,
  isInMill,
  parseMuehleState,
  phaseLabels,
  phaseOf,
  removableStones,
  stonesOnBoard,
  targetsOf,
  type MuehleMove,
  type MuehleState,
} from "./muehle";

type MuehleGameProps = {
  session: SessionDto;
  /** Called with the new session after a move or when the state has to be reloaded. */
  onChanged: (session?: SessionDto) => void;
};

type Pending = { from: number | null; to: number };

/** Stones of one player: colour, name, stones still to place and on the board. */
const PlayerInfo: React.FC<{ state: MuehleState; seat: number; name: string; active: boolean }> = ({ state, seat, name, active }) => (
  <Paper
    variant="outlined"
    sx={{ flex: 1, minWidth: 0, p: 1.25, display: "flex", gap: 1, alignItems: "center", borderWidth: 2, borderColor: active ? "primary.main" : "divider" }}
  >
    <Avatar sx={{ width: 30, height: 30, bgcolor: seat === 0 ? "#fffdf7" : "#2b2250", border: "2px solid", borderColor: seat === 0 ? "#8a7a99" : "#120c2b" }}> </Avatar>
    <Stack sx={{ minWidth: 0 }}>
      <Typography noWrap sx={{ fontWeight: 800 }}>
        {name}
      </Typography>
      <Typography variant="body2" color="text.secondary" noWrap>
        {state.inHand[seat] > 0 ? `${state.inHand[seat]} zu setzen · ` : ""}
        {stonesOnBoard(state.board, seat)} auf dem Brett
      </Typography>
    </Stack>
  </Paper>
);

/** Mühle: board with tap controls (select, move, take a stone after a mill), status, result and rematch. */
const MuehleGame: React.FC<MuehleGameProps> = ({ session, onChanged }) => {
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [aiThinkingSeat, setAiThinkingSeat] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [rematchId, setRematchId] = React.useState<string | null>(null);
  const [selected, setSelected] = React.useState<number | null>(null);
  const [pending, setPending] = React.useState<Pending | null>(null);
  const [version, setVersion] = React.useState(session.stateVersion);

  // A new state from the server resets the local selection.
  if (version !== session.stateVersion) {
    setVersion(session.stateVersion);
    setSelected(null);
    setPending(null);
  }

  useGameSessionEvents(session.id, {
    onAiThinking: (message) => setAiThinkingSeat(message.seatId),
    onActionApplied: () => setAiThinkingSeat(null),
    onRematchCreated: (message) => setRematchId(message.newSessionId),
  });

  const state = parseMuehleState(session.stateJson);
  if (!state) {
    return <Alert severity="error">Der Spielstand kann nicht angezeigt werden.</Alert>;
  }

  const seats = [...session.seats].sort((a, b) => a.order - b.order);
  const mySeat = seats.find((seat) => seat.id === session.mySeatId);
  const currentSeat = seats.find((seat) => seat.id === session.currentSeatId);
  const isMyTurn = session.status === "Running" && state.status === "Running" && currentSeat !== undefined && currentSeat.id === mySeat?.id;
  const isEnded = session.status === "Finished" || session.status === "Aborted" || state.status !== "Running";
  const me = mySeat?.order ?? 0;
  const color = colorOf(me);
  const phase = phaseOf(state, me);

  const submit = async (move: MuehleMove) => {
    setIsSubmitting(true);
    setError(null);
    try {
      await sendRequest<unknown, SubmitSessionActionRequest>("POST", {
        serviceUrl: sessionUrls.actions(session.id),
        body: { expectedVersion: session.stateVersion, actionType: "MuehleMove", payload: JSON.stringify(move) },
      });
      onChanged();
    } catch (failure) {
      setSelected(null);
      setPending(null);
      if (failure instanceof ApiError && failure.status === 409) {
        onChanged();
      } else {
        setError(getErrorMessage(failure, "Der Zug konnte nicht ausgeführt werden."));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // Board shown to the player: with a pending step (mill closed, stone to take) already applied.
  const shownBoard = pending ? applyStep(state.board, pending.from, pending.to, color) : state.board;
  const removable = pending ? removableStones(shownBoard, 1 - me) : [];
  const from = phase === "Placing" ? null : selected;
  const targets = isMyTurn && !pending ? targetsOf(state, me, from) : [];
  const ownStones = [...state.board].flatMap((point, index) => (point === color ? [index] : []));
  const movable = phase === "Placing" ? [] : ownStones.filter((point) => targetsOf(state, me, point).length > 0);
  const clickable = !isMyTurn || isSubmitting ? [] : pending ? removable : [...targets, ...movable];

  const step = (to: number) => {
    const after = applyStep(state.board, from, to, color);
    if (isInMill(after, to) && removableStones(after, 1 - me).length > 0) {
      setPending({ from, to });
    } else {
      void submit({ from, to, remove: null });
    }
  };

  const onPoint = (point: number) => {
    if (pending) {
      void submit({ from: pending.from, to: pending.to, remove: point });
    } else if (state.board[point] === color) {
      setSelected(selected === point ? null : point);
    } else if (state.board[point] === emptyPoint && targets.includes(point)) {
      step(point);
    }
  };

  const resultText = (() => {
    if (session.status === "Aborted") {
      return "Die Partie wurde abgebrochen.";
    }
    const reason = state.endReason ? ` (${endReasonLabels[state.endReason] ?? state.endReason})` : "";
    if (state.status === "Draw") {
      return `Unentschieden${reason}.`;
    }
    const winner = seats.find((seat) => seat.order === state.winnerSeat);
    if (state.status === "Won" && winner) {
      return winner.id === mySeat?.id ? `Du hast gewonnen${reason}!` : `${winner.displayName} hat gewonnen${reason}.`;
    }
    return mySeat?.connectionStatus === "Removed" ? "Du wurdest aus der Partie entfernt." : "Du hast gewonnen – dein Gegner wurde entfernt.";
  })();
  const isWin = resultText.startsWith("Du hast gewonnen");

  const statusText = (() => {
    if (session.status === "Paused") {
      return "Die Partie ist pausiert.";
    }
    if (aiThinkingSeat && aiThinkingSeat === currentSeat?.id) {
      return "KI denkt …";
    }
    if (!isMyTurn) {
      return currentSeat ? `${currentSeat.displayName} ist am Zug.` : "";
    }
    if (pending) {
      return "Mühle! Tippe einen gegnerischen Stein an, um ihn zu entfernen.";
    }
    if (phase === "Placing") {
      return `Du bist am Zug (${colorName(me)}): setze einen Stein.`;
    }
    if (selected === null) {
      return `Du bist am Zug (${colorName(me)}): wähle einen Stein${phase === "Flying" ? " – du darfst springen" : ""}.`;
    }
    return "Tippe auf ein markiertes Ziel.";
  })();

  return (
    <Stack spacing={2} sx={{ width: "100%", maxWidth: 440, mx: "auto" }}>
      {!isEnded && (
        <GameStatusBar
          text={statusText}
          busy={Boolean(aiThinkingSeat && aiThinkingSeat === currentSeat?.id) || isSubmitting}
          highlight={isMyTurn}
          deadline={session.turnDeadline}
        />
      )}

      <Stack direction="row" spacing={1}>
        {seats.slice(0, 2).map((seat) => (
          <PlayerInfo
            key={seat.id}
            state={state}
            seat={seat.order}
            name={seat.id === mySeat?.id ? "Du" : seat.displayName}
            active={!isEnded && seat.id === currentSeat?.id}
          />
        ))}
      </Stack>

      <MuehleBoard
        board={shownBoard}
        clickable={clickable}
        targets={targets}
        removable={removable}
        selected={pending ? pending.to : from}
        lastMove={pending ? null : state.lastMove}
        onPoint={onPoint}
      />

      {!isEnded && (
        <Stack direction="row" spacing={1} sx={{ justifyContent: "center", flexWrap: "wrap" }}>
          <Chip label={`Phase: ${phaseLabels[phase]}`} color="primary" variant="outlined" />
          {state.inHand[0] === 0 && state.inHand[1] === 0 && (
            <Chip label={`${Math.floor(state.pliesWithoutCapture / 2)}/50 Züge ohne Schlagen`} variant="outlined" />
          )}
          {pending && (
            <Button size="small" onClick={() => setPending(null)}>
              Zug zurücknehmen
            </Button>
          )}
        </Stack>
      )}

      {error && <Alert severity="error">{error}</Alert>}

      {isEnded && <GameEndActions session={session} rematchId={rematchId} result={resultText} isWin={isWin} />}
    </Stack>
  );
};

export default MuehleGame;
