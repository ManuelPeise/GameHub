import React from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import IconButton from "@mui/material/IconButton";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Slider from "@mui/material/Slider";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import FirstPage from "@mui/icons-material/FirstPage";
import LastPage from "@mui/icons-material/LastPage";
import NavigateBefore from "@mui/icons-material/NavigateBefore";
import NavigateNext from "@mui/icons-material/NavigateNext";
import ReplayOutlined from "@mui/icons-material/ReplayOutlined";
import ListAltOutlined from "@mui/icons-material/ListAltOutlined";
import EmojiEventsOutlined from "@mui/icons-material/EmojiEventsOutlined";
import {
  gameNames,
  outcomeLabels,
  sessionUrls,
  type GameHistoryDetailDto,
  type HistoryActionDto,
  type SeatDto,
} from "../../api/sessions";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import { categoryLabels, parseKniffelState } from "../../games/kniffel/kniffel";
import KniffelSheetTable from "../../games/kniffel/KniffelSheetTable";
import Loading from "../../components/Loading";
import Page from "../../components/Page";
import SectionCard from "../../components/SectionCard";

type HistoryDetailPageProps = {
  sessionId: string;
};

const systemLabels: Record<string, string> = {
  Disconnected: "Verbindung unterbrochen",
  Reconnected: "wieder verbunden",
  PlayerRemoved: "aus der Partie entfernt",
  TurnTimeout: "Zugzeit abgelaufen",
  Paused: "Partie pausiert",
  Resumed: "Partie fortgesetzt",
};

const cellNames = ["oben links", "oben Mitte", "oben rechts", "Mitte links", "Mitte", "Mitte rechts", "unten links", "unten Mitte", "unten rechts"];

const parseJson = (json: string | null | undefined): Record<string, unknown> | null => {
  if (!json) {
    return null;
  }
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
};

const describeAction = (action: HistoryActionDto): string => {
  if (systemLabels[action.actionType]) {
    const reason = parseJson(action.payloadJson)?.reason;
    return typeof reason === "string" ? `${systemLabels[action.actionType]} (${reason})` : systemLabels[action.actionType];
  }

  const cell = parseJson(action.payloadJson)?.cell;
  if (action.actionType === "PlaceMark" && typeof cell === "number") {
    return `setzt auf ${cellNames[cell] ?? `Feld ${cell}`}`;
  }

  const payload = parseJson(action.payloadJson);
  const heldMask = payload?.heldMask;
  const category = payload?.category;
  if (action.actionType === "Roll" && action.randomResultJson) {
    const held = typeof heldMask === "number" ? heldMask : 0;
    const draws = (JSON.parse(action.randomResultJson) as number[]).map((draw) => draw + 1);
    return held ? `würfelt ${draws.join(", ")} (Würfel gehalten)` : `würfelt ${draws.join(", ")}`;
  }
  if (action.actionType === "Score" && typeof category === "string" && category in categoryLabels) {
    return `wertet ${categoryLabels[category as keyof typeof categoryLabels]}`;
  }

  const random = action.randomResultJson ? ` – Zufall: ${action.randomResultJson}` : "";
  return `${action.actionType}${action.payloadJson ? ` ${action.payloadJson}` : ""}${random}`;
};

/** Read-only view of a game state: Tic-Tac-Toe board, Kniffel dice and score sheets, otherwise JSON. */
const StateView: React.FC<{ gameType: string; stateJson: string; seats: SeatDto[] }> = ({ gameType, stateJson, seats }) => {
  const state = parseJson(stateJson);
  const board = state?.board;
  if (gameType === "TicTacToe" && typeof board === "string") {
    const winningLine = state?.winningLine;
    const winning = Array.isArray(winningLine) ? (winningLine as number[]) : [];
    return (
      <Box
        role="grid"
        aria-label="Spielfeld"
        sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 1, width: "100%", maxWidth: 280, mx: "auto", aspectRatio: "1" }}
      >
        {[...board].map((cell, index) => (
          <Paper
            key={index}
            role="gridcell"
            variant="outlined"
            aria-label={`${cellNames[index]}: ${cell === "-" ? "leer" : cell}`}
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: "2.4rem",
              fontWeight: 800,
              color: cell === "X" ? "primary.main" : "secondary.main",
              borderColor: winning.includes(index) ? "secondary.main" : "divider",
              borderWidth: 2,
            }}
          >
            {cell === "-" ? "" : cell}
          </Paper>
        ))}
      </Box>
    );
  }

  const kniffel = gameType === "Kniffel" ? parseKniffelState(stateJson) : null;
  if (kniffel) {
    return (
      <Stack spacing={1.5}>
        <Stack direction="row" spacing={1} aria-label="Würfel" sx={{ justifyContent: "center", flexWrap: "wrap" }}>
          {kniffel.dice.some((die) => die > 0) ? (
            kniffel.dice.map((die, index) => <Chip key={index} label={die} color={(kniffel.heldMask & (1 << index)) !== 0 ? "primary" : "default"} />)
          ) : (
            <Typography color="text.secondary">noch nicht gewürfelt</Typography>
          )}
          <Chip variant="outlined" label={`Wurf ${kniffel.rollsUsed} von 3`} />
        </Stack>
        <KniffelSheetTable state={kniffel} seats={seats} />
      </Stack>
    );
  }

  return (
    <Box component="pre" sx={{ maxHeight: 320, overflow: "auto", fontSize: "0.8rem" }}>
      {JSON.stringify(state, null, 2)}
    </Box>
  );
};

/** Move log of a finished game with a step-by-step replay (forward and back). */
const HistoryDetailPage: React.FC<HistoryDetailPageProps> = ({ sessionId }) => {
  const [detail, setDetail] = React.useState<GameHistoryDetailDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [step, setStep] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    sendRequest<GameHistoryDetailDto>("GET", { serviceUrl: sessionUrls.historyDetail(sessionId) })
      .then((loaded) => {
        if (!cancelled && loaded) {
          setDetail(loaded);
          setStep(loaded.actions.length);
        }
      })
      .catch((failure: unknown) => {
        if (!cancelled) {
          setError(getErrorMessage(failure, "Die Partie konnte nicht geladen werden."));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  if (!detail) {
    return <Page title="Partieverlauf">{error ? <Alert severity="error">{error}</Alert> : <Loading />}</Page>;
  }

  const { entry, actions } = detail;
  const seats: SeatDto[] = entry.seats.map((seat) => ({
    id: seat.seatId,
    order: seat.order,
    kind: seat.kind,
    displayName: seat.displayName,
    connectionStatus: seat.outcome === "Removed" ? "Removed" : "Connected",
  }));
  const seatName = (seatId?: string | null) => entry.seats.find((seat) => seat.seatId === seatId)?.displayName ?? "System";
  const stateJson = step === 0 ? detail.initialStateJson : actions[step - 1].stateJson;
  const current = step > 0 ? actions[step - 1] : null;

  return (
    <Page
      title={gameNames[entry.gameType] ?? entry.gameType}
      subtitle={`${entry.mode === "Singleplayer" ? "gegen die KI" : "Mehrspieler"} · ${new Date(entry.endedAt).toLocaleString("de-DE", { dateStyle: "medium", timeStyle: "short" })}`}
      width="lg"
    >
      <SectionCard title="Ergebnis" icon={<EmojiEventsOutlined color="secondary" />}>
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
          {[...entry.seats]
            .sort((a, b) => (a.placement ?? 99) - (b.placement ?? 99))
            .map((seat) => (
              <Chip
                key={seat.seatId}
                color={seat.outcome === "Win" ? "success" : seat.outcome === "Draw" ? "info" : "default"}
                variant={seat.isMe ? "filled" : "outlined"}
                label={`${seat.displayName}${seat.isMe ? " (du)" : ""}: ${seat.outcome ? outcomeLabels[seat.outcome] : "–"}${
                  seat.score !== null && seat.score !== undefined ? ` · ${seat.score} P.` : ""
                }`}
              />
            ))}
        </Stack>
      </SectionCard>

      <SectionCard title="Nachspielen" icon={<ReplayOutlined color="primary" />}>
        <Typography color="text.secondary" sx={{ minHeight: 24 }}>
          {current ? `${step}. ${seatName(current.seatId)}: ${describeAction(current)}` : "Startposition"}
        </Typography>
        <StateView gameType={entry.gameType} stateJson={stateJson} seats={seats} />
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <IconButton onClick={() => setStep(0)} disabled={step === 0} aria-label="Zum Anfang">
            <FirstPage />
          </IconButton>
          <IconButton onClick={() => setStep(step - 1)} disabled={step === 0} aria-label="Ein Schritt zurück">
            <NavigateBefore />
          </IconButton>
          <Slider
            value={step}
            min={0}
            max={actions.length}
            step={1}
            onChange={(_, value) => setStep(value as number)}
            aria-label="Schritt wählen"
            valueLabelDisplay="auto"
            sx={{ mx: 1 }}
          />
          <IconButton onClick={() => setStep(step + 1)} disabled={step === actions.length} aria-label="Ein Schritt vor">
            <NavigateNext />
          </IconButton>
          <IconButton onClick={() => setStep(actions.length)} disabled={step === actions.length} aria-label="Zum Ende">
            <LastPage />
          </IconButton>
        </Stack>
      </SectionCard>

      <SectionCard title={`Alle Aktionen (${actions.length})`} icon={<ListAltOutlined color="primary" />}>
        <List dense disablePadding sx={{ maxHeight: { xs: 360, md: 480 }, overflowY: "auto" }}>
          {actions.map((action, index) => (
            <ListItemButton key={action.sequence} selected={index + 1 === step} onClick={() => setStep(index + 1)} sx={{ borderRadius: 2 }}>
              <ListItemText
                primary={`${seatName(action.seatId)}: ${describeAction(action)}`}
                secondary={`${action.sequence}. · ${new Date(action.createdAt).toLocaleTimeString("de-DE")}`}
              />
            </ListItemButton>
          ))}
        </List>
      </SectionCard>
    </Page>
  );
};

export default HistoryDetailPage;
