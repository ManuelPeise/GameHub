import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import AddCircleOutline from "@mui/icons-material/AddCircleOutline";
import VpnKeyOutlined from "@mui/icons-material/VpnKeyOutlined";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import {
  sessionPagePaths,
  sessionUrls,
  type JoinSessionRequest,
  type OpenLobbyDto,
  type SessionDto,
} from "../api/sessions";
import Page from "../components/Page";
import OpenLobbiesSection from "../components/OpenLobbiesSection";
import SectionCard from "../components/SectionCard";

/** Public list of open multiplayer lobbies; join by click or by invite code. */
const OpenLobbiesPage: React.FC = () => {
  const navigate = useNavigate();
  const [inviteCode, setInviteCode] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [isJoining, setIsJoining] = React.useState(false);

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

      <OpenLobbiesSection onJoin={(lobby) => joinById(lobby)} disabled={isJoining} />

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
