import React from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import ButtonBase from "@mui/material/ButtonBase";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import Casino from "@mui/icons-material/Casino";
import { ApiError, getErrorMessage, sendRequest } from "../../hooks/apiClient";
import { useGameSessionEvents } from "../../hooks/gameHub";
import { sessionUrls, type SessionDto, type SubmitSessionActionRequest } from "../../api/sessions";
import { GameEndActions, GameStatusBar } from "../GameParts";
import KniffelSheetTable from "./KniffelSheetTable";
import { categories, maxRolls, parseKniffelState, total } from "./kniffel";

type KniffelGameProps = {
  session: SessionDto;
  onChanged: (session?: SessionDto) => void;
};

/** Pip positions on a 3×3 grid per face. */
const pipLayout: Record<number, number[]> = {
  1: [4],
  2: [0, 8],
  3: [0, 4, 8],
  4: [0, 2, 6, 8],
  5: [0, 2, 4, 6, 8],
  6: [0, 2, 3, 5, 6, 8],
};

const Die: React.FC<{ value: number; held: boolean }> = ({ value, held }) => (
  <Box
    sx={{
      display: "grid",
      gridTemplateColumns: "repeat(3, 1fr)",
      gridTemplateRows: "repeat(3, 1fr)",
      width: "100%",
      height: "100%",
      p: "16%",
    }}
  >
    {Array.from({ length: 9 }, (_, index) => (
      <Box
        key={index}
        sx={{
          m: "auto",
          width: "70%",
          aspectRatio: "1",
          borderRadius: "50%",
          bgcolor: pipLayout[value]?.includes(index) ? (held ? "#ffffff" : "#2b2250") : "transparent",
        }}
      />
    ))}
  </Box>
);

/** Kniffel table: dice with hold selection, roll button, all score sheets, result and new round. */
const KniffelGame: React.FC<KniffelGameProps> = ({ session, onChanged }) => {
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [aiThinkingSeat, setAiThinkingSeat] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [rematchId, setRematchId] = React.useState<string | null>(null);
  // Hold selection belongs to one state version; after every action the server state is the default again.
  const [hold, setHold] = React.useState<{ version: number; mask: number } | null>(null);

  useGameSessionEvents(session.id, {
    onAiThinking: (message) => setAiThinkingSeat(message.seatId),
    onActionApplied: () => setAiThinkingSeat(null),
    onRematchCreated: (message) => setRematchId(message.newSessionId),
  });

  const state = parseKniffelState(session.stateJson);
  if (!state) {
    return <Alert severity="error">Der Spielstand kann nicht angezeigt werden.</Alert>;
  }

  const seats = [...session.seats].sort((a, b) => a.order - b.order);
  const mySeat = seats.find((seat) => seat.id === session.mySeatId);
  const currentSeat = seats.find((seat) => seat.order === state.currentSeat);
  const isRunning = session.status === "Running" && state.status === "Running";
  const isMyTurn = isRunning && mySeat !== undefined && currentSeat?.id === mySeat.id;
  const heldMask = hold && hold.version === session.stateVersion ? hold.mask : state.heldMask;
  const canHold = isMyTurn && state.rollsUsed > 0 && state.rollsUsed < maxRolls;
  const isEnded = session.status === "Finished" || session.status === "Aborted";
  const aiThinking = Boolean(aiThinkingSeat && aiThinkingSeat === currentSeat?.id);

  const send = async (actionType: string, payload: unknown) => {
    setIsSubmitting(true);
    setError(null);
    try {
      await sendRequest<unknown, SubmitSessionActionRequest>("POST", {
        serviceUrl: sessionUrls.actions(session.id),
        body: { expectedVersion: session.stateVersion, actionType, payload: JSON.stringify(payload) },
      });
      onChanged();
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 409) {
        onChanged();
      } else {
        setError(getErrorMessage(failure, "Die Aktion konnte nicht ausgeführt werden."));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const toggle = (index: number) => {
    if (canHold) {
      setHold({ version: session.stateVersion, mask: heldMask ^ (1 << index) });
    }
  };

  const ranking = seats
    .filter((seat) => !state.removed[seat.order])
    .map((seat) => ({ seat, points: total(state.sheets[seat.order]) }))
    .sort((a, b) => b.points - a.points);
  const best = ranking[0]?.points;
  const winners = ranking.filter((entry) => entry.points === best);

  const resultText =
    session.status === "Aborted"
      ? "Die Partie wurde abgebrochen."
      : winners.length > 1
        ? "Unentschieden."
        : winners[0]?.seat.id === mySeat?.id
          ? "Du hast gewonnen!"
          : `${winners[0]?.seat.displayName ?? "–"} hat gewonnen.`;

  const statusText = (() => {
    if (session.status === "Paused") {
      return "Die Partie ist pausiert.";
    }
    if (aiThinking) {
      return `${currentSeat?.displayName} denkt …`;
    }
    if (isMyTurn) {
      return state.rollsUsed === 0
        ? "Du bist am Zug – würfle!"
        : state.rollsUsed < maxRolls
          ? "Tippe Würfel zum Halten an, würfle erneut oder werte unten."
          : "Wähle eine Kategorie im Block.";
    }
    return currentSeat ? `${currentSeat.displayName} ist am Zug.` : "";
  })();

  const rollDisabled = !isMyTurn || state.rollsUsed >= maxRolls || isSubmitting || (state.rollsUsed > 0 && heldMask === 31);

  return (
    <Stack spacing={2} sx={{ width: "100%", maxWidth: 720, mx: "auto" }}>
      {!isEnded && <GameStatusBar text={statusText} busy={aiThinking || isSubmitting} highlight={isMyTurn} deadline={session.turnDeadline} />}

      {isRunning && (
        <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 } }}>
          <Stack spacing={1.5}>
            <Stack direction="row" spacing={{ xs: 1, sm: 1.5 }} role="group" aria-label="Würfel" sx={{ justifyContent: "center" }}>
              {state.dice.map((die, index) => {
                const isHeld = state.rollsUsed > 0 && (heldMask & (1 << index)) !== 0;
                return (
                  <ButtonBase
                    key={index}
                    onClick={() => toggle(index)}
                    disabled={!canHold || isSubmitting}
                    aria-pressed={isHeld}
                    aria-label={die === 0 ? "noch nicht gewürfelt" : `Würfel ${die}${isHeld ? ", gehalten" : ""}`}
                    sx={{
                      flex: "1 1 0",
                      maxWidth: 72,
                      aspectRatio: "1",
                      borderRadius: 2.5,
                      bgcolor: die === 0 ? "action.hover" : isHeld ? "primary.main" : "#ffffff",
                      border: 3,
                      borderColor: isHeld ? "primary.dark" : "divider",
                      transform: isHeld ? "translateY(-6px)" : "none",
                      boxShadow: die === 0 ? "none" : 3,
                      transition: "transform 120ms, background-color 120ms",
                    }}
                  >
                    {die > 0 && <Die value={die} held={isHeld} />}
                  </ButtonBase>
                );
              })}
            </Stack>
            <Button
              variant="contained"
              size="large"
              startIcon={<Casino />}
              onClick={() => void send("Roll", { heldMask: state.rollsUsed === 0 ? 0 : heldMask })}
              disabled={rollDisabled}
              fullWidth
            >
              Würfeln ({maxRolls - state.rollsUsed} übrig)
            </Button>
          </Stack>
        </Paper>
      )}

      {error && <Alert severity="error">{error}</Alert>}

      {isEnded && (
        <>
          <GameEndActions session={session} rematchId={rematchId} result={resultText} isWin={winners.length === 1 && winners[0]?.seat.id === mySeat?.id} />
          <Paper variant="outlined">
            <Typography variant="h3" sx={{ px: 2, pt: 2 }}>
              Endwertung
            </Typography>
            <List dense>
              {ranking.map((entry) => (
                <ListItem key={entry.seat.id}>
                  <ListItemText
                    primary={`${1 + ranking.filter((other) => other.points > entry.points).length}. ${entry.seat.id === mySeat?.id ? "Du" : entry.seat.displayName}`}
                  />
                  <Typography sx={{ fontWeight: 700 }}>{entry.points} Punkte</Typography>
                </ListItem>
              ))}
              {seats
                .filter((seat) => state.removed[seat.order])
                .map((seat) => (
                  <ListItem key={seat.id}>
                    <ListItemText primary={seat.displayName} secondary="aus der Wertung gestrichen" sx={{ opacity: 0.6 }} />
                  </ListItem>
                ))}
            </List>
          </Paper>
        </>
      )}

      <KniffelSheetTable
        state={state}
        seats={seats}
        mySeatId={session.mySeatId}
        onScore={isMyTurn && state.rollsUsed > 0 && !isSubmitting ? (index) => void send("Score", { category: categories[index] }) : undefined}
      />
    </Stack>
  );
};

export default KniffelGame;
