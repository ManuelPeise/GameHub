import React from "react";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import List from "@mui/material/List";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Typography from "@mui/material/Typography";
import Casino from "@mui/icons-material/Casino";
import GridOn from "@mui/icons-material/GridOn";
import {
  gameNames,
  outcomeLabels,
  sessionPagePaths,
  sessionUrls,
  type GameHistoryEntryDto,
} from "../../api/sessions";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import Loading from "../../components/Loading";
import Page from "../../components/Page";
import SectionCard from "../../components/SectionCard";

const pageSize = 20;

const formatDate = (value: string) =>
  new Date(value).toLocaleString("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  });

const resultChip = (entry: GameHistoryEntryDto) => {
  if (entry.status === "Aborted") {
    return <Chip size="small" label="abgebrochen" />;
  }
  const outcome = entry.myOutcome;
  const color =
    outcome === "Win"
      ? "success"
      : outcome === "Draw"
        ? "info"
        : outcome
          ? "error"
          : "default";
  return (
    <Chip
      size="small"
      color={color}
      label={outcome ? outcomeLabels[outcome] : "–"}
    />
  );
};

/** List of the own finished and aborted games. */
const HistoryPage: React.FC = () => {
  const [entries, setEntries] = React.useState<GameHistoryEntryDto[] | null>(
    null,
  );
  const [hasMore, setHasMore] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(async (skip: number) => {
    try {
      const page =
        (await sendRequest<GameHistoryEntryDto[]>("GET", {
          serviceUrl: sessionUrls.history,
          params: { skip, take: pageSize },
        })) ?? [];
      setEntries((previous) =>
        skip === 0 ? page : [...(previous ?? []), ...page],
      );
      setHasMore(page.length === pageSize);
    } catch (failure) {
      setError(
        getErrorMessage(
          failure,
          "Der Spielverlauf konnte nicht geladen werden.",
        ),
      );
    }
  }, []);

  React.useEffect(() => {
    const onLoad = async () => {
      await load(0);
    };
    void onLoad();
  }, [load]);

  return (
    <Page
      title="Spielverlauf"
      subtitle="Deine beendeten Partien – tippe eine an, um sie nachzuspielen."
    >
      {error && <Alert severity="error">{error}</Alert>}
      <SectionCard>
        {entries === null && !error && <Loading />}
        {entries?.length === 0 && (
          <Typography color="text.secondary">
            Du hast noch keine beendeten Partien.
          </Typography>
        )}
        {entries && entries.length > 0 && (
          <List disablePadding>
            {entries.map((entry) => {
              const opponents = entry.seats
                .filter((seat) => !seat.isMe)
                .map((seat) => seat.displayName)
                .join(", ");
              return (
                <ListItemButton
                  key={entry.sessionId}
                  href={`${sessionPagePaths.historyPrefix}${entry.sessionId}`}
                  divider
                  sx={{ gap: 1, px: 1 }}
                >
                  <ListItemAvatar sx={{ minWidth: 48 }}>
                    <Avatar sx={{ bgcolor: "background.default" }}>
                      {entry.gameType === "Kniffel" ? (
                        <Casino color="secondary" />
                      ) : (
                        <GridOn color="primary" />
                      )}
                    </Avatar>
                  </ListItemAvatar>
                  <ListItemText
                    primary={`${gameNames[entry.gameType] ?? entry.gameType} gegen ${opponents || "–"}`}
                    secondary={formatDate(entry.endedAt)}
                    slotProps={{ primary: { noWrap: true } }}
                    sx={{ minWidth: 0 }}
                  />
                  {resultChip(entry)}
                </ListItemButton>
              );
            })}
          </List>
        )}
        {hasMore && entries && (
          <Button
            onClick={() => void load(entries.length)}
            sx={{ alignSelf: "center" }}
          >
            Weitere laden
          </Button>
        )}
      </SectionCard>
    </Page>
  );
};

export default HistoryPage;
