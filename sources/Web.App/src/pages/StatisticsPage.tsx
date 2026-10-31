import { alpha, type Theme } from "@mui/material/styles";
import React from "react";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Grid from "@mui/material/Grid";
import LinearProgress from "@mui/material/LinearProgress";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import EmojiEventsOutlined from "@mui/icons-material/EmojiEventsOutlined";
import InsightsOutlined from "@mui/icons-material/InsightsOutlined";
import StarOutline from "@mui/icons-material/StarOutline";
import type { GameMode, LeaderboardDto, PlayerStatisticsDto, StatisticsRowDto } from "../api/generated/contracts";
import { difficultyOptions, gameNames, gameTypes } from "../api/sessions";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import Loading from "../components/Loading";
import Page from "../components/Page";
import SectionCard from "../components/SectionCard";

const games = gameTypes;
type Game = (typeof games)[number];
type Period = "all" | "30d";

const difficultyLabel = (value?: string | null) => difficultyOptions.find((option) => option.value === value)?.label ?? value ?? "";
const modeLabel = (mode: GameMode, difficulty?: string | null) =>
  mode === "Multiplayer" ? "Mehrspieler" : `gegen KI · ${difficultyLabel(difficulty)}`;
const percent = (value: number) => `${Math.round(value * 100)} %`;
const medal = (rank: number) => (rank === 1 ? "#ffd54f" : rank === 2 ? "#cfd8dc" : rank === 3 ? "#d7a26c" : undefined);

const Stat = ({ label, value }: { label: string; value: React.ReactNode }) => (
  <Box sx={{ textAlign: "center", minWidth: 56 }}>
    <Typography sx={{ fontWeight: 800, fontSize: "1.25rem" }}>{value}</Typography>
    <Typography variant="caption" color="text.secondary">
      {label}
    </Typography>
  </Box>
);

/** One mode of a game as compact card (mobile first: stacks, side by side from tablet width). */
const StatisticsTile = ({ row }: { row: StatisticsRowDto }) => {
  const winRate = row.games > 0 ? row.wins / row.games : 0;
  return (
    <Paper variant="outlined" sx={{ p: 2, height: "100%" }}>
      <Typography sx={{ fontWeight: 700, mb: 1 }}>{modeLabel(row.mode, row.aiDifficulty)}</Typography>
      <Stack direction="row" sx={{ justifyContent: "space-between", flexWrap: "wrap", rowGap: 1 }}>
        <Stat label="Partien" value={row.games} />
        <Stat label="Siege" value={row.wins} />
        <Stat label="Niederl." value={row.losses} />
        <Stat label="Unent." value={row.draws} />
        {row.averageScore !== null && row.averageScore !== undefined && <Stat label="Ø Punkte" value={row.averageScore} />}
        {row.bestScore !== null && row.bestScore !== undefined && <Stat label="Bestwert" value={row.bestScore} />}
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", mt: 1.5 }}>
        <LinearProgress variant="determinate" value={winRate * 100} color="success" sx={{ flexGrow: 1, height: 6, borderRadius: 3 }} />
        <Typography variant="body2" color="text.secondary">
          {percent(winRate)}
        </Typography>
      </Stack>
    </Paper>
  );
};

/** Personal statistics per game and the multiplayer leaderboard. */
const StatisticsPage: React.FC = () => {
  const [personal, setPersonal] = React.useState<PlayerStatisticsDto | null>(null);
  const [board, setBoard] = React.useState<LeaderboardDto | null>(null);
  const [game, setGame] = React.useState<Game>("TicTacToe");
  const [period, setPeriod] = React.useState<Period>("all");
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    sendRequest<PlayerStatisticsDto>("GET", { serviceUrl: "/api/statistics/me" })
      .then(setPersonal)
      .catch((failure: unknown) => setError(getErrorMessage(failure, "Die Statistik konnte nicht geladen werden.")));
  }, []);

  React.useEffect(() => {
    let cancelled = false;
    sendRequest<LeaderboardDto>("GET", { serviceUrl: "/api/statistics/leaderboard", params: { gameType: game, period } })
      .then((loaded) => {
        if (!cancelled) {
          setBoard(loaded);
        }
      })
      .catch((failure: unknown) => {
        if (!cancelled) {
          setError(getErrorMessage(failure, "Die Bestenliste konnte nicht geladen werden."));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [game, period]);

  const rows = personal?.rows.filter((row) => row.gameType === game) ?? [];

  return (
    <Page title="Statistik" width="lg">
      {error && <Alert severity="error">{error}</Alert>}

      <Tabs value={game} onChange={(_, value: Game) => setGame(value)} variant="fullWidth" sx={{ borderBottom: 1, borderColor: "divider" }}>
        {games.map((gameType) => (
          <Tab key={gameType} value={gameType} label={gameNames[gameType]} />
        ))}
      </Tabs>

      <SectionCard title="Meine Ergebnisse" icon={<InsightsOutlined color="primary" />}>
        {personal === null && !error && <Loading />}
        {personal && rows.length === 0 && (
          <Typography color="text.secondary">Noch keine beendeten Partien {gameNames[game]}.</Typography>
        )}
        {rows.length > 0 && (
          <Grid container spacing={1.5}>
            {rows.map((row) => (
              <Grid key={`${row.mode}-${row.aiDifficulty ?? ""}`} size={{ xs: 12, sm: 6, lg: 4 }}>
                <StatisticsTile row={row} />
              </Grid>
            ))}
          </Grid>
        )}
        <Typography variant="caption" color="text.secondary">
          Aus der Partie entfernt zählt als Niederlage; abgebrochene Partien zählen nicht.
        </Typography>
      </SectionCard>

      <SectionCard
        title="Bestenliste"
        icon={<EmojiEventsOutlined color="secondary" />}
        action={
          <ToggleButtonGroup size="small" exclusive value={period} onChange={(_, value: Period | null) => value && setPeriod(value)}>
            <ToggleButton value="all">Gesamt</ToggleButton>
            <ToggleButton value="30d">30 Tage</ToggleButton>
          </ToggleButtonGroup>
        }
      >
        <Typography variant="body2" color="text.secondary">
          Nur Mehrspieler-Partien · meiste Siege, dann Siegquote
        </Typography>
        {board && board.entries.length === 0 && <Typography color="text.secondary">Noch keine Siege in diesem Zeitraum.</Typography>}
        {board && board.entries.length > 0 && (
          <List disablePadding>
            {board.entries.map((entry, index) => (
              <ListItem
                key={index}
                divider
                disableGutters
                sx={{ bgcolor: entry.isMe ? ((t: Theme) => alpha(t.palette.primary.main, 0.1)) : undefined, borderRadius: 2, px: 1 }}
              >
                <ListItemAvatar sx={{ minWidth: 48 }}>
                  <Avatar sx={{ width: 34, height: 34, fontWeight: 800, bgcolor: medal(entry.rank) ?? "background.default", color: medal(entry.rank) ? "#111" : "text.primary" }}>
                    {entry.rank}
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={entry.isMe ? `${entry.displayName} (du)` : entry.displayName}
                  secondary={`${entry.games} Partien · ${percent(entry.winRate)} Siegquote`}
                />
                <Typography sx={{ fontWeight: 800 }}>{entry.wins}</Typography>
                <Typography variant="caption" color="text.secondary" sx={{ ml: 0.5 }}>
                  Siege
                </Typography>
              </ListItem>
            ))}
          </List>
        )}
      </SectionCard>

      {game === "Kniffel" && board && board.highScores.length > 0 && (
        <SectionCard title="Höchste Punktzahlen" icon={<StarOutline color="secondary" />}>
          <List disablePadding>
            {board.highScores.map((score, index) => (
              <ListItem key={index} divider disableGutters sx={{ bgcolor: score.isMe ? ((t: Theme) => alpha(t.palette.primary.main, 0.1)) : undefined, borderRadius: 2, px: 1 }}>
                <ListItemAvatar sx={{ minWidth: 48 }}>
                  <Avatar sx={{ width: 34, height: 34, fontWeight: 800, bgcolor: medal(score.rank) ?? "background.default", color: medal(score.rank) ? "#111" : "text.primary" }}>
                    {score.rank}
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={score.isMe ? `${score.displayName} (du)` : score.displayName}
                  secondary={new Date(score.achievedAt).toLocaleDateString("de-DE")}
                />
                <Typography sx={{ fontWeight: 800 }}>{score.score}</Typography>
              </ListItem>
            ))}
          </List>
        </SectionCard>
      )}
    </Page>
  );
};

export default StatisticsPage;
