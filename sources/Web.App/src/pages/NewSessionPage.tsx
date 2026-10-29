import React from "react";
import { useNavigate, useSearchParams } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import FormControl from "@mui/material/FormControl";
import FormLabel from "@mui/material/FormLabel";
import IconButton from "@mui/material/IconButton";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import Casino from "@mui/icons-material/Casino";
import FlagOutlined from "@mui/icons-material/FlagOutlined";
import GridOn from "@mui/icons-material/GridOn";
import GroupsOutlined from "@mui/icons-material/GroupsOutlined";
import PlayArrow from "@mui/icons-material/PlayArrow";
import PlayCircleOutline from "@mui/icons-material/PlayCircleOutline";
import SmartToyOutlined from "@mui/icons-material/SmartToyOutlined";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import {
  difficultyOptions,
  gameNames,
  sessionPagePaths,
  sessionUrls,
  type CreateSessionRequest,
  type SessionDto,
} from "../api/sessions";
import Page from "../components/Page";
import SectionCard from "../components/SectionCard";
import StatusChip from "../components/StatusChip";

type GameType = "TicTacToe" | "Kniffel";
type Mode = "Singleplayer" | "Multiplayer";

/** Create a game: choose game, mode, difficulty and (Tic-Tac-Toe) X or O. Lists own open games. */
const NewSessionPage: React.FC = () => {
  const navigate = useNavigate();
  const [query] = useSearchParams();
  const [gameType, setGameType] = React.useState<GameType>(query.get("game") === "Kniffel" ? "Kniffel" : "TicTacToe");
  const [mode, setMode] = React.useState<Mode>(query.get("mode") === "Multiplayer" ? "Multiplayer" : "Singleplayer");
  const [difficulty, setDifficulty] = React.useState("Easy");
  const [preferredSeat, setPreferredSeat] = React.useState(0);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [openSessions, setOpenSessions] = React.useState<SessionDto[]>([]);

  const loadOpen = React.useCallback(async () => {
    try {
      setOpenSessions((await sendRequest<SessionDto[]>("GET", { serviceUrl: sessionUrls.mine })) ?? []);
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
      await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.abandon(session.id) });
      await loadOpen();
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Partie konnte nicht aufgegeben werden."));
    }
  };

  const openSingleplayer = openSessions.find((session) => session.mode === "Singleplayer" && session.gameType === gameType);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    const request: CreateSessionRequest = {
      gameType,
      mode,
      difficulty: mode === "Singleplayer" ? difficulty : null,
      preferredSeat: mode === "Singleplayer" && gameType === "TicTacToe" ? preferredSeat : 0,
    };

    try {
      const created = await sendRequest<SessionDto, CreateSessionRequest>("POST", { serviceUrl: sessionUrls.base, body: request });
      if (created) {
        navigate(`${sessionPagePaths.lobbyPrefix}${created.id}`);
      } else {
        setError("Die Partie konnte nicht erstellt werden.");
      }
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Partie konnte nicht erstellt werden. Bitte versuche es erneut."));
    } finally {
      setIsSubmitting(false);
    }
  };

  const choice = <T extends string | number>(label: string, value: T, onChange: (value: T) => void, options: { value: T; label: React.ReactNode }[]) => (
    <FormControl>
      <FormLabel sx={{ mb: 1 }}>{label}</FormLabel>
      <ToggleButtonGroup
        exclusive
        color="primary"
        value={value}
        onChange={(_, next: T | null) => next !== null && onChange(next)}
        sx={{ flexWrap: "wrap" }}
      >
        {options.map((option) => (
          <ToggleButton key={String(option.value)} value={option.value} sx={{ px: 2.5, gap: 1 }}>
            {option.label}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
    </FormControl>
  );

  return (
    <Page title="Neue Partie" subtitle="Wähle Spiel und Gegner.">
      {openSessions.length > 0 && (
        <SectionCard title="Deine offenen Partien" icon={<PlayCircleOutline color="primary" />}>
          {mode === "Singleplayer" && openSingleplayer && (
            <Alert severity="info">
              Du hast bereits eine offene Partie {gameNames[gameType]} gegen die KI. „Partie starten“ öffnet sie; gib sie auf, um neu
              zu beginnen.
            </Alert>
          )}
          <List disablePadding>
            {openSessions.map((session) => (
              <ListItem
                key={session.id}
                disablePadding
                secondaryAction={
                  session.mode === "Singleplayer" && (
                    <Tooltip title="Aufgeben">
                      <IconButton edge="end" aria-label="Partie aufgeben" onClick={() => void abandon(session)}>
                        <FlagOutlined />
                      </IconButton>
                    </Tooltip>
                  )
                }
              >
                <ListItemButton href={`${sessionPagePaths.lobbyPrefix}${session.id}`} sx={{ borderRadius: 2 }}>
                  <ListItemText
                    primary={gameNames[session.gameType] ?? session.gameType}
                    secondary={session.mode === "Singleplayer" ? "gegen die KI" : "Mehrspieler"}
                  />
                  <StatusChip status={session.status} />
                </ListItemButton>
              </ListItem>
            ))}
          </List>
        </SectionCard>
      )}

      <SectionCard>
        <Stack component="form" spacing={3} onSubmit={submit}>
          {choice<GameType>("Spiel", gameType, setGameType, [
            { value: "TicTacToe", label: <><GridOn fontSize="small" /> Tic-Tac-Toe</> },
            { value: "Kniffel", label: <><Casino fontSize="small" /> Kniffel</> },
          ])}
          {choice<Mode>("Gegner", mode, setMode, [
            { value: "Singleplayer", label: <><SmartToyOutlined fontSize="small" /> Gegen die KI</> },
            { value: "Multiplayer", label: <><GroupsOutlined fontSize="small" /> Mit Freunden</> },
          ])}
          {mode === "Singleplayer" &&
            choice<string>(
              "Schwierigkeitsgrad",
              difficulty,
              setDifficulty,
              difficultyOptions.map((option) => ({ value: option.value, label: option.label })),
            )}
          {mode === "Singleplayer" &&
            gameType === "TicTacToe" &&
            choice<number>("Du spielst", preferredSeat, setPreferredSeat, [
              { value: 0, label: "X – du beginnst" },
              { value: 1, label: "O – die KI beginnt" },
            ])}
          {mode === "Multiplayer" && (
            <Typography color="text.secondary">
              Du bekommst eine Lobby mit Einladungscode. {gameType === "Kniffel" ? "Kniffel spielen 2 bis 4 Personen" : "Tic-Tac-Toe spielen 2 Personen"};
              in der Lobby kannst du auch KI-Mitspieler hinzufügen.
            </Typography>
          )}
          {error && <Alert severity="error">{error}</Alert>}
          <Button type="submit" variant="contained" size="large" startIcon={<PlayArrow />} loading={isSubmitting} sx={{ alignSelf: "flex-start" }}>
            Partie starten
          </Button>
        </Stack>
      </SectionCard>
    </Page>
  );
};

export default NewSessionPage;
