import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import AddCircleOutline from "@mui/icons-material/AddCircleOutline";
import Casino from "@mui/icons-material/Casino";
import GridOn from "@mui/icons-material/GridOn";
import GroupsOutlined from "@mui/icons-material/GroupsOutlined";
import VpnKeyOutlined from "@mui/icons-material/VpnKeyOutlined";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import {
  gameNames,
  sessionPagePaths,
  sessionUrls,
  type JoinSessionRequest,
  type OpenLobbyDto,
  type SessionDto,
} from "../api/sessions";
import Loading from "../components/Loading";
import Page from "../components/Page";
import SectionCard from "../components/SectionCard";

const pollIntervalMs = 5000;

/** Public list of open multiplayer lobbies; join by click or by invite code. */
const OpenLobbiesPage: React.FC = () => {
  const navigate = useNavigate();
  const [lobbies, setLobbies] = React.useState<OpenLobbyDto[] | null>(null);
  const [inviteCode, setInviteCode] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [isJoining, setIsJoining] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const open = await sendRequest<OpenLobbyDto[]>("GET", { serviceUrl: sessionUrls.open });
        if (!cancelled) {
          setLobbies(open ?? []);
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

  const join = async (request: () => Promise<SessionDto | null>) => {
    setIsJoining(true);
    setError(null);
    try {
      const session = await request();
      if (session) {
        navigate(`${sessionPagePaths.lobbyPrefix}${session.id}`);
      }
    } catch (failure) {
      setError(getErrorMessage(failure, "Beitritt fehlgeschlagen."));
    } finally {
      setIsJoining(false);
    }
  };

  const joinById = (lobby: OpenLobbyDto) => {
    if (lobby.isMine) {
      navigate(`${sessionPagePaths.lobbyPrefix}${lobby.id}`);
      return;
    }
    void join(() => sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.joinById(lobby.id) }));
  };

  const joinByCode = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void join(() =>
      sendRequest<SessionDto, JoinSessionRequest>("POST", { serviceUrl: sessionUrls.join, body: { inviteCode: inviteCode.trim() } }),
    );
  };

  return (
    <Page
      title="Offene Partien"
      subtitle="Tritt einer Lobby bei oder gib einen Einladungscode ein."
      actions={
        <Button href={sessionPagePaths.newSession} variant="contained" startIcon={<AddCircleOutline />}>
          Neue Partie
        </Button>
      }
    >
      {error && <Alert severity="error">{error}</Alert>}

      <SectionCard title="Lobbys" icon={<GroupsOutlined color="primary" />}>
        {lobbies === null && <Loading />}
        {lobbies?.length === 0 && <Typography color="text.secondary">Zurzeit gibt es keine offenen Partien.</Typography>}
        {lobbies && lobbies.length > 0 && (
          <List disablePadding>
            {lobbies.map((lobby) => (
              <ListItem key={lobby.id} divider disableGutters sx={{ gap: 1.5, flexWrap: "wrap" }}>
                <ListItemAvatar sx={{ minWidth: 0 }}>
                  <Avatar sx={{ bgcolor: "background.default" }}>
                    {lobby.gameType === "Kniffel" ? <Casino color="secondary" /> : <GridOn color="primary" />}
                  </Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={gameNames[lobby.gameType] ?? lobby.gameType}
                  secondary={`von ${lobby.ownerName}${lobby.isMine ? " (du)" : ""} · ${lobby.seatCount}/${lobby.maxSeats} Spieler`}
                  sx={{ flex: "1 1 160px" }}
                />
                <Button
                  variant={lobby.isMine ? "outlined" : "contained"}
                  onClick={() => joinById(lobby)}
                  disabled={isJoining}
                  sx={{ flex: { xs: "1 1 100%", sm: "0 0 auto" } }}
                >
                  {lobby.isMine ? "Öffnen" : "Beitreten"}
                </Button>
              </ListItem>
            ))}
          </List>
        )}
      </SectionCard>

      <SectionCard title="Mit Code beitreten" icon={<VpnKeyOutlined color="primary" />}>
        <Stack component="form" direction={{ xs: "column", sm: "row" }} spacing={2} onSubmit={joinByCode}>
          <TextField
            label="Einladungscode"
            value={inviteCode}
            onChange={(event) => setInviteCode(event.target.value.toUpperCase())}
            autoComplete="off"
            slotProps={{ htmlInput: { style: { letterSpacing: 4, fontFamily: "monospace" } } }}
          />
          <Button type="submit" variant="contained" disabled={isJoining || inviteCode.trim().length === 0} sx={{ flexShrink: 0 }}>
            Beitreten
          </Button>
        </Stack>
      </SectionCard>
    </Page>
  );
};

export default OpenLobbiesPage;
