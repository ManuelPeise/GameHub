import React from "react";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import InputAdornment from "@mui/material/InputAdornment";
import List from "@mui/material/List";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import Search from "@mui/icons-material/Search";
import SportsEsportsOutlined from "@mui/icons-material/SportsEsportsOutlined";
import type { AdminGameDto } from "../../api/generated/contracts";
import { gameNames } from "../../api/sessions";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import GameIcon from "../../games/GameIcon";
import Loading from "../../components/Loading";
import SectionCard from "../../components/SectionCard";
import StatusChip from "../../components/StatusChip";
import { adminGamePath, adminUrls, formatDateTime } from "./adminApi";

const pageSize = 25;

/** All games (except lobbies): search by player or game id, replay of every game (US-026). */
const GamesPanel: React.FC = () => {
  const [query, setQuery] = React.useState("");
  const [games, setGames] = React.useState<AdminGameDto[] | null>(null);
  const [hasMore, setHasMore] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(
    async (skip: number) => {
      try {
        const page =
          (await sendRequest<AdminGameDto[]>("GET", { serviceUrl: adminUrls.games, params: { query: query.trim(), skip, take: pageSize } })) ?? [];
        setGames((previous) => (skip === 0 ? page : [...(previous ?? []), ...page]));
        setHasMore(page.length === pageSize);
      } catch (failure) {
        setError(getErrorMessage(failure, "Die Partien konnten nicht geladen werden."));
      }
    },
    [query],
  );

  React.useEffect(() => {
    const timer = setTimeout(() => void load(0), 300);
    return () => clearTimeout(timer);
  }, [load]);

  return (
    <SectionCard title="Spielverläufe" icon={<SportsEsportsOutlined color="primary" />}>
      <TextField
        size="small"
        placeholder="Spieler (Name/E-Mail) oder Partie-ID"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <Search />
              </InputAdornment>
            ),
          },
        }}
      />
      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {games === null && !error && <Loading />}
      {games?.length === 0 && <Typography color="text.secondary">Keine Partien gefunden.</Typography>}
      {games && games.length > 0 && (
        <List disablePadding>
          {games.map((game) => (
            <ListItemButton key={game.sessionId} href={adminGamePath(game.sessionId)} divider sx={{ gap: 1, px: 1 }}>
              <ListItemAvatar sx={{ minWidth: 48 }}>
                <Avatar sx={{ bgcolor: "background.default" }}>
                  <GameIcon gameType={game.gameType} color={game.gameType === "TicTacToe" ? "primary" : "secondary"} />
                </Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={`${gameNames[game.gameType] ?? game.gameType}: ${game.players.join(", ")}`}
                secondary={`${game.mode === "Singleplayer" ? "Singleplayer" : "Mehrspieler"} · ${formatDateTime(game.endedAt)}`}
                slotProps={{ primary: { noWrap: true } }}
                sx={{ minWidth: 0 }}
              />
              <StatusChip status={game.status} />
            </ListItemButton>
          ))}
        </List>
      )}
      {hasMore && games && (
        <Button onClick={() => void load(games.length)} sx={{ alignSelf: "center" }}>
          Weitere laden
        </Button>
      )}
    </SectionCard>
  );
};

export default GamesPanel;
