import React from "react";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemText from "@mui/material/ListItemText";
import Typography from "@mui/material/Typography";
import GroupsOutlined from "@mui/icons-material/GroupsOutlined";
import { gameNames, sessionUrls, type OpenLobbyDto } from "../api/sessions";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import GameIcon from "../games/GameIcon";
import Loading from "./Loading";
import SectionCard from "./SectionCard";

const pollIntervalMs = 5000;

type OpenLobbiesSectionProps = {
  onJoin: (lobby: OpenLobbyDto) => void;
  disabled?: boolean;
  action?: React.ReactNode;
};

/** Public list of open multiplayer lobbies, refreshed every few seconds (start page and lobby page). */
const OpenLobbiesSection: React.FC<OpenLobbiesSectionProps> = ({ onJoin, disabled = false, action }) => {
  const [lobbies, setLobbies] = React.useState<OpenLobbyDto[] | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const open = await sendRequest<OpenLobbyDto[]>("GET", { serviceUrl: sessionUrls.open });
        if (!cancelled) {
          setLobbies(open ?? []);
          setError(null);
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

  return (
    <SectionCard title="Offene Partien" icon={<GroupsOutlined color="primary" />} action={action}>
      {error && <Alert severity="error">{error}</Alert>}
      {lobbies === null && !error && <Loading />}
      {lobbies?.length === 0 && <Typography color="text.secondary">Zurzeit wartet niemand – starte selbst eine Partie mit Freunden!</Typography>}
      {lobbies && lobbies.length > 0 && (
        <List disablePadding>
          {lobbies.map((lobby) => (
            <ListItem key={lobby.id} divider disableGutters sx={{ gap: 1.5, flexWrap: "wrap" }}>
              <ListItemAvatar sx={{ minWidth: 0 }}>
                <Avatar sx={{ bgcolor: lobby.gameType === "TicTacToe" ? "primary.main" : lobby.gameType === "Muehle" ? "success.main" : "secondary.main" }}>
                  <GameIcon gameType={lobby.gameType} />
                </Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={gameNames[lobby.gameType] ?? lobby.gameType}
                secondary={`von ${lobby.ownerName}${lobby.isMine ? " (du)" : ""} · ${lobby.seatCount}/${lobby.maxSeats} Spieler`}
                slotProps={{ primary: { sx: { fontWeight: 800 } } }}
                sx={{ flex: "1 1 160px" }}
              />
              <Button
                variant={lobby.isMine ? "outlined" : "contained"}
                color={lobby.isMine ? "primary" : "success"}
                onClick={() => onJoin(lobby)}
                disabled={disabled}
                sx={{ flex: { xs: "1 1 100%", sm: "0 0 auto" } }}
              >
                {lobby.isMine ? "Öffnen" : "Mitspielen"}
              </Button>
            </ListItem>
          ))}
        </List>
      )}
    </SectionCard>
  );
};

export default OpenLobbiesSection;
