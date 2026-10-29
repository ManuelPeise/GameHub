import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import EmojiEventsOutlined from "@mui/icons-material/EmojiEventsOutlined";
import HistoryOutlined from "@mui/icons-material/HistoryOutlined";
import Replay from "@mui/icons-material/Replay";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import { sessionPagePaths, sessionUrls, type SessionDto } from "../api/sessions";
import TurnTimer from "../components/TurnTimer";

type StatusBarProps = {
  text: React.ReactNode;
  /** Shows a spinner (e.g. "KI denkt …"). */
  busy?: boolean;
  /** Highlights the bar when it is the viewer's turn. */
  highlight?: boolean;
  deadline?: string | null;
};

/** Status line above the board: whose turn, AI thinking, result; with the turn timer. */
export const GameStatusBar: React.FC<StatusBarProps> = ({ text, busy, highlight, deadline }) => (
  <Paper
    variant="outlined"
    role="status"
    aria-live="polite"
    sx={{
      px: 2,
      py: 1.5,
      display: "flex",
      alignItems: "center",
      gap: 1.5,
      flexWrap: "wrap",
      borderColor: highlight ? "primary.main" : "divider",
      bgcolor: highlight ? "rgba(157, 123, 255, 0.08)" : "background.paper",
    }}
  >
    {busy && <CircularProgress size={18} />}
    <Typography sx={{ fontWeight: 600, flexGrow: 1 }}>{text}</Typography>
    <TurnTimer deadline={deadline} />
  </Paper>
);

type EndActionsProps = {
  session: SessionDto;
  /** Id of a new round someone else already started. */
  rematchId: string | null;
  result: React.ReactNode;
  isWin?: boolean;
};

/** Result and "Neue Runde" / history after the end of a game. */
export const GameEndActions: React.FC<EndActionsProps> = ({ session, rematchId, result, isWin }) => {
  const navigate = useNavigate();
  const [error, setError] = React.useState<string | null>(null);
  const [isStarting, setIsStarting] = React.useState(false);

  const rematch = async () => {
    setError(null);
    setIsStarting(true);
    try {
      const next = await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.rematch(session.id) });
      if (next) {
        navigate(`${sessionPagePaths.lobbyPrefix}${next.id}`);
      }
    } catch (failure) {
      setError(getErrorMessage(failure, "Die neue Runde konnte nicht gestartet werden."));
    } finally {
      setIsStarting(false);
    }
  };

  return (
    <Stack spacing={2} sx={{ alignItems: "stretch" }}>
      <Alert severity={isWin ? "success" : "info"} icon={isWin ? <EmojiEventsOutlined /> : undefined} sx={{ fontWeight: 600 }}>
        {result}
        {rematchId && " Eine neue Runde wurde gestartet."}
      </Alert>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction="row" spacing={1} sx={{ "& > *": { flex: 1 } }}>
        {rematchId ? (
          <Button variant="contained" href={`${sessionPagePaths.lobbyPrefix}${rematchId}`} startIcon={<Replay />}>
            Zur neuen Runde
          </Button>
        ) : (
          <Button variant="contained" onClick={() => void rematch()} loading={isStarting} startIcon={<Replay />}>
            Neue Runde
          </Button>
        )}
        <Button variant="outlined" href={`${sessionPagePaths.historyPrefix}${session.id}`} startIcon={<HistoryOutlined />}>
          Verlauf
        </Button>
      </Stack>
    </Stack>
  );
};
