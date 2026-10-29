import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Typography from "@mui/material/Typography";
import AdminPanelSettingsOutlined from "@mui/icons-material/AdminPanelSettingsOutlined";
import { ApiError, sendRequest } from "../../hooks/apiClient";
import { accountUrls, pagePaths, roleNames, type CurrentUserResponse } from "../../api/account";
import { useCurrentUser } from "../../app/CurrentUser";
import Loading from "../../components/Loading";
import Page from "../../components/Page";
import SectionCard from "../../components/SectionCard";
import TwoFactorSetup from "./TwoFactorSetup";

type State =
  | { kind: "loading" }
  | { kind: "signedOut" }
  | { kind: "noAccess" }
  | { kind: "twoFactorSetup" }
  | { kind: "twoFactorLogin" }
  | { kind: "ready"; user: CurrentUserResponse };

/** Admin area: only for administrators with two-factor sign-in (checked by the API). */
const AdminPage: React.FC = () => {
  const navigate = useNavigate();
  const { logout } = useCurrentUser();
  const [state, setState] = React.useState<State>({ kind: "loading" });

  const check = React.useCallback(async () => {
    let user: CurrentUserResponse | null;
    try {
      user = await sendRequest<CurrentUserResponse>("GET", { serviceUrl: accountUrls.me });
    } catch {
      setState({ kind: "signedOut" });
      return;
    }

    if (!user || !user.roles.includes(roleNames.administrator)) {
      setState({ kind: "noAccess" });
      return;
    }

    try {
      await sendRequest<unknown>("GET", { serviceUrl: accountUrls.adminStatus });
      setState({ kind: "ready", user });
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 403) {
        // Administrator without two-factor sign-in: set it up, or sign in again with the second factor.
        setState({ kind: user.twoFactorEnabled ? "twoFactorLogin" : "twoFactorSetup" });
      } else {
        setState({ kind: "noAccess" });
      }
    }
  }, []);

  React.useEffect(() => {
    const onCheck = async () => {
      await check();
    };
    void onCheck();
  }, [check]);

  const signOutAndLogin = async () => {
    await logout();
    navigate(pagePaths.login);
  };

  return (
    <Page title="Administration" subtitle="Spieler, Logs, Backups und Konfiguration">
      {state.kind === "loading" && <Loading />}
      {state.kind === "signedOut" && (
        <Alert severity="info" action={<Button href={pagePaths.login}>Anmelden</Button>}>
          Bitte melde dich an.
        </Alert>
      )}
      {state.kind === "noAccess" && <Alert severity="error">Kein Zugriff. Dieser Bereich ist nur für Administratoren.</Alert>}
      {state.kind === "twoFactorSetup" && <TwoFactorSetup onCompleted={() => void check()} />}
      {state.kind === "twoFactorLogin" && (
        <Alert
          severity="warning"
          action={
            <Button color="inherit" onClick={() => void signOutAndLogin()}>
              Erneut anmelden
            </Button>
          }
        >
          Bitte melde dich erneut an und bestätige mit dem Code aus deiner Authenticator-App.
        </Alert>
      )}
      {state.kind === "ready" && (
        <SectionCard title="Übersicht" icon={<AdminPanelSettingsOutlined color="primary" />}>
          <Typography>Angemeldet als {state.user.displayName} mit Zwei-Faktor-Anmeldung.</Typography>
          <Typography color="text.secondary">Die Verwaltungsfunktionen folgen mit den nächsten Stories.</Typography>
        </SectionCard>
      )}
    </Page>
  );
};

export default AdminPage;
