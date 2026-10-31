import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardActions from "@mui/material/CardActions";
import CardContent from "@mui/material/CardContent";
import Grid from "@mui/material/Grid";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import Casino from "@mui/icons-material/Casino";
import GridOn from "@mui/icons-material/GridOn";
import PlayCircleOutline from "@mui/icons-material/PlayCircleOutline";
import SmartToyOutlined from "@mui/icons-material/SmartToyOutlined";
import GroupsOutlined from "@mui/icons-material/GroupsOutlined";
import { pagePaths } from "../api/account";
import { gameNames, sessionPagePaths, sessionUrls, type OpenLobbyDto, type SessionDto } from "../api/sessions";
import { useCurrentUser } from "../app/CurrentUser";
import Loading from "../components/Loading";
import OpenLobbiesSection from "../components/OpenLobbiesSection";
import Page from "../components/Page";
import SectionCard from "../components/SectionCard";
import StatusChip from "../components/StatusChip";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";

const games = [
  {
    type: "TicTacToe",
    icon: <GridOn sx={{ fontSize: 44 }} />,
    gradient: "linear-gradient(135deg, #6c4cff 0%, #9b5cff 100%)",
    text: "Der Klassiker auf dem 3×3-Feld – gegen die KI in drei Stufen oder gegen Freunde.",
  },
  {
    type: "Kniffel",
    icon: <Casino sx={{ fontSize: 44 }} />,
    gradient: "linear-gradient(135deg, #ff8a00 0%, #ff5f8f 100%)",
    text: "Fünf Würfel, drei Würfe, 13 Kategorien – allein gegen die KI oder mit bis zu vier Spielern.",
  },
] as const;

/** Colourful round badge for a game icon. */
const GameBadge: React.FC<{ gradient: string; children: React.ReactNode }> = ({ gradient, children }) => (
  <Box
    sx={{
      width: 68,
      height: 68,
      flexShrink: 0,
      borderRadius: "22px",
      display: "grid",
      placeItems: "center",
      color: "#ffffff",
      background: gradient,
      boxShadow: "0 4px 0 rgba(43, 34, 80, 0.15)",
      transform: "rotate(-4deg)",
    }}
  >
    {children}
  </Box>
);

const newSessionLink = (game: string, mode: "Singleplayer" | "Multiplayer") =>
  `${sessionPagePaths.newSession}?game=${game}&mode=${mode}`;

/** Start page: games to start, open lobbies to join, own open games; signed out a short introduction. */
const HomePage: React.FC = () => {
  const { user } = useCurrentUser();
  const navigate = useNavigate();
  const [openSessions, setOpenSessions] = React.useState<SessionDto[] | null>(null);
  const [joinError, setJoinError] = React.useState<string | null>(null);
  const [isJoining, setIsJoining] = React.useState(false);

  const joinLobby = async (lobby: OpenLobbyDto) => {
    if (lobby.isMine) {
      navigate(`${sessionPagePaths.lobbyPrefix}${lobby.id}`);
      return;
    }
    setIsJoining(true);
    setJoinError(null);
    try {
      const session = await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.joinById(lobby.id) });
      if (session) {
        navigate(`${sessionPagePaths.lobbyPrefix}${session.id}`);
      }
    } catch (failure) {
      setJoinError(getErrorMessage(failure, "Beitritt fehlgeschlagen."));
    } finally {
      setIsJoining(false);
    }
  };

  const lobbies = (
    <>
      {joinError && (
        <Alert severity="error" onClose={() => setJoinError(null)}>
          {joinError}
        </Alert>
      )}
      <OpenLobbiesSection
        onJoin={(lobby) => void joinLobby(lobby)}
        disabled={isJoining}
        action={
          <Button href={sessionPagePaths.openLobbies} size="small">
            Mit Code beitreten
          </Button>
        }
      />
    </>
  );

  React.useEffect(() => {
    if (!user) {
      return;
    }
    sendRequest<SessionDto[]>("GET", { serviceUrl: sessionUrls.mine })
      .then((sessions) => setOpenSessions(sessions ?? []))
      .catch(() => setOpenSessions([]));
  }, [user]);

  if (user === undefined) {
    return <Loading />;
  }

  if (user === null) {
    return (
      <Page title="Willkommen bei GameHub" subtitle="Tic-Tac-Toe und Kniffel – gegen die KI oder mit Freunden.">
        <Grid container spacing={2}>
          {games.map((game) => (
            <Grid key={game.type} size={{ xs: 12, sm: 6 }}>
              <Card sx={{ height: "100%" }}>
                <CardContent>
                  <Stack direction="row" spacing={2} sx={{ alignItems: "center", mb: 1 }}>
                    <GameBadge gradient={game.gradient}>{game.icon}</GameBadge>
                    <Typography variant="h3">{gameNames[game.type]}</Typography>
                  </Stack>
                  <Typography color="text.secondary">{game.text}</Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
        <Stack direction="row" spacing={2}>
          <Button href={pagePaths.login} variant="contained" size="large">
            Anmelden
          </Button>
          <Button href={pagePaths.register} variant="outlined" size="large">
            Konto anlegen
          </Button>
        </Stack>
      </Page>
    );
  }

  return (
    <Page title={`Hallo, ${user.displayName}!`} subtitle="Worauf hast du Lust?" width="lg">
      <Grid container spacing={2}>
        {games.map((game) => (
          <Grid key={game.type} size={{ xs: 12, md: 6 }}>
            <Card sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
              <CardContent sx={{ flexGrow: 1 }}>
                <Stack direction="row" spacing={2} sx={{ alignItems: "center", mb: 1 }}>
                  <GameBadge gradient={game.gradient}>{game.icon}</GameBadge>
                  <Typography variant="h2">{gameNames[game.type]}</Typography>
                </Stack>
                <Typography color="text.secondary">{game.text}</Typography>
              </CardContent>
              <CardActions sx={{ p: 2, pt: 0, gap: 1, flexWrap: "wrap" }}>
                <Button href={newSessionLink(game.type, "Singleplayer")} variant="contained" color={game.type === "Kniffel" ? "secondary" : "primary"} startIcon={<SmartToyOutlined />}>
                  Gegen die KI
                </Button>
                <Button href={newSessionLink(game.type, "Multiplayer")} variant="outlined" startIcon={<GroupsOutlined />}>
                  Mit Freunden
                </Button>
              </CardActions>
            </Card>
          </Grid>
        ))}
      </Grid>

      {lobbies}

      <SectionCard
        title="Deine offenen Partien"
        icon={<PlayCircleOutline color="primary" />}
        action={
          <Button href={sessionPagePaths.openLobbies} size="small">
            Alle Lobbys
          </Button>
        }
      >
        {openSessions === null && <Loading />}
        {openSessions?.length === 0 && (
          <Typography color="text.secondary">Keine offenen Partien – starte oben eine neue.</Typography>
        )}
        {openSessions && openSessions.length > 0 && (
          <List disablePadding>
            {openSessions.map((session) => (
              <ListItemButton key={session.id} href={`${sessionPagePaths.lobbyPrefix}${session.id}`} sx={{ borderRadius: 2 }}>
                <ListItemText
                  primary={gameNames[session.gameType] ?? session.gameType}
                  secondary={
                    session.mode === "Singleplayer"
                      ? "gegen die KI"
                      : session.seats.map((seat) => seat.displayName).join(", ")
                  }
                />
                <Box sx={{ ml: 2 }}>
                  <StatusChip status={session.status} />
                </Box>
              </ListItemButton>
            ))}
          </List>
        )}
      </SectionCard>
    </Page>
  );
};

export default HomePage;
