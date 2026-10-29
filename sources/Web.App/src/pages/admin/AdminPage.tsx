import React from "react";
import { ApiError, sendRequest } from "../../hooks/apiClient";
import {
  accountUrls,
  pagePaths,
  roleNames,
  type CurrentUserResponse,
} from "../../api/account";
import TwoFactorSetup from "./TwoFactorSetup";
import "../account/AccountPages.css";

type State =
  | { kind: "loading" }
  | { kind: "signedOut" }
  | { kind: "noAccess" }
  | { kind: "twoFactorSetup" }
  | { kind: "twoFactorLogin" }
  | { kind: "ready"; user: CurrentUserResponse };

/** Admin area: only for administrators with two-factor sign-in (checked by the API). */
const AdminPage: React.FC = () => {
  const [state, setState] = React.useState<State>({ kind: "loading" });

  const check = React.useCallback(async () => {
    let user: CurrentUserResponse | null;
    try {
      user = await sendRequest<CurrentUserResponse>("GET", {
        serviceUrl: accountUrls.me,
      });
    } catch {
      setState({ kind: "signedOut" });
      return;
    }

    if (!user || !user.roles.includes(roleNames.administrator)) {
      setState({ kind: "noAccess" });
      return;
    }

    try {
      await sendRequest<unknown>("GET", {
        serviceUrl: accountUrls.adminStatus,
      });
      setState({ kind: "ready", user });
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 403) {
        // Administrator without two-factor sign-in: set it up, or sign in again with the second factor.
        setState({
          kind: user.twoFactorEnabled ? "twoFactorLogin" : "twoFactorSetup",
        });
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
    await sendRequest<null>("POST", { serviceUrl: accountUrls.logout });
    window.location.assign(pagePaths.login);
  };

  return (
    <main className="account-page">
      <h1>Administration</h1>
      {state.kind === "loading" && <p>Lädt …</p>}
      {state.kind === "signedOut" && (
        <p className="account-links">
          <a href={pagePaths.login}>Bitte melde dich an.</a>
        </p>
      )}
      {state.kind === "noAccess" && (
        <p className="account-error" role="alert">
          Kein Zugriff. Dieser Bereich ist nur für Administratoren.
        </p>
      )}
      {state.kind === "twoFactorSetup" && (
        <TwoFactorSetup onCompleted={() => void check()} />
      )}
      {state.kind === "twoFactorLogin" && (
        <div className="account-form">
          <p>
            Bitte melde dich erneut an und bestätige mit dem Code aus deiner
            Authenticator-App.
          </p>
          <button type="button" onClick={() => void signOutAndLogin()}>
            Erneut anmelden
          </button>
        </div>
      )}
      {state.kind === "ready" && (
        <p className="account-success" role="status">
          Angemeldet als {state.user.displayName}. Die Verwaltungsfunktionen
          folgen.
        </p>
      )}
      <p className="account-links">
        <a href={pagePaths.home}>Zur Startseite</a>
      </p>
    </main>
  );
};

export default AdminPage;
