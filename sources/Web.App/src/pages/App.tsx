import React from "react";
import { pagePaths } from "../api/account";
import { sessionPagePaths } from "../api/sessions";
import DeleteAccountPage from "./account/DeleteAccountPage";
import EmailConfirmedPage from "./account/EmailConfirmedPage";
import ForgotPasswordPage from "./account/ForgotPasswordPage";
import LoginPage from "./account/LoginPage";
import RegisterPage from "./account/RegisterPage";
import ResetPasswordPage from "./account/ResetPasswordPage";
import AdminPage from "./admin/AdminPage";
import HomePage from "./HomePage";
import LobbyPage from "./LobbyPage";
import NewSessionPage from "./NewSessionPage";
import OpenLobbiesPage from "./OpenLobbiesPage";
import HistoryDetailPage from "./history/HistoryDetailPage";
import HistoryPage from "./history/HistoryPage";

/** Minimal path-based page selection until routing and layout arrive. */
const App: React.FC = () => {
  if (window.location.pathname === sessionPagePaths.newSession) {
    return <NewSessionPage />;
  }

  if (window.location.pathname === sessionPagePaths.history) {
    return <HistoryPage />;
  }

  if (window.location.pathname.startsWith(sessionPagePaths.historyPrefix)) {
    return <HistoryDetailPage sessionId={window.location.pathname.slice(sessionPagePaths.historyPrefix.length)} />;
  }

  if (window.location.pathname === sessionPagePaths.openLobbies) {
    return <OpenLobbiesPage />;
  }

  if (window.location.pathname.startsWith(sessionPagePaths.lobbyPrefix)) {
    const sessionId = window.location.pathname.slice(sessionPagePaths.lobbyPrefix.length);
    return <LobbyPage sessionId={sessionId} />;
  }

  if (window.location.pathname.startsWith(sessionPagePaths.joinPrefix)) {
    const inviteCode = window.location.pathname.slice(sessionPagePaths.joinPrefix.length);
    return <LobbyPage inviteCode={inviteCode} />;
  }

  switch (window.location.pathname) {
    case pagePaths.login:
      return <LoginPage />;
    case pagePaths.register:
      return <RegisterPage />;
    case pagePaths.forgotPassword:
      return <ForgotPasswordPage />;
    case pagePaths.resetPassword:
      return <ResetPasswordPage />;
    case pagePaths.emailConfirmed:
      return <EmailConfirmedPage />;
    case pagePaths.deleteAccount:
      return <DeleteAccountPage />;
    case pagePaths.admin:
      return <AdminPage />;
    default:
      return <HomePage />;
  }
};

export default App;
