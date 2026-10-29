import { Route, Routes, useParams } from "react-router";
import { pagePaths } from "../api/account";
import { sessionPagePaths } from "../api/sessions";
import AppLayout from "../app/AppLayout";
import DeleteAccountPage from "./account/DeleteAccountPage";
import EmailConfirmedPage from "./account/EmailConfirmedPage";
import ForgotPasswordPage from "./account/ForgotPasswordPage";
import LoginPage from "./account/LoginPage";
import RegisterPage from "./account/RegisterPage";
import ResetPasswordPage from "./account/ResetPasswordPage";
import AdminPage from "./admin/AdminPage";
import HistoryDetailPage from "./history/HistoryDetailPage";
import HistoryPage from "./history/HistoryPage";
import HomePage from "./HomePage";
import LobbyPage from "./LobbyPage";
import NewSessionPage from "./NewSessionPage";
import NotFoundPage from "./NotFoundPage";
import OpenLobbiesPage from "./OpenLobbiesPage";
import StatisticsPage from "./StatisticsPage";

// Route parameters; the key renders a fresh page when the session changes (e.g. "Neue Runde").
const SessionRoute = () => {
  const { sessionId = "" } = useParams();
  return <LobbyPage key={sessionId} sessionId={sessionId} />;
};

const JoinRoute = () => {
  const { inviteCode = "" } = useParams();
  return <LobbyPage key={inviteCode} inviteCode={inviteCode} />;
};

const HistoryDetailRoute = () => {
  const { sessionId = "" } = useParams();
  return <HistoryDetailPage key={sessionId} sessionId={sessionId} />;
};

/** All pages inside the common layout. */
const App = () => (
  <Routes>
    <Route element={<AppLayout />}>
      <Route index element={<HomePage />} />
      <Route path={pagePaths.login} element={<LoginPage />} />
      <Route path={pagePaths.register} element={<RegisterPage />} />
      <Route path={pagePaths.forgotPassword} element={<ForgotPasswordPage />} />
      <Route path={pagePaths.resetPassword} element={<ResetPasswordPage />} />
      <Route path={pagePaths.emailConfirmed} element={<EmailConfirmedPage />} />
      <Route path={pagePaths.deleteAccount} element={<DeleteAccountPage />} />
      <Route path={pagePaths.admin} element={<AdminPage />} />
      <Route path={sessionPagePaths.newSession} element={<NewSessionPage />} />
      <Route path={sessionPagePaths.openLobbies} element={<OpenLobbiesPage />} />
      <Route path={`${sessionPagePaths.lobbyPrefix}:sessionId`} element={<SessionRoute />} />
      <Route path={`${sessionPagePaths.joinPrefix}:inviteCode`} element={<JoinRoute />} />
      <Route path={sessionPagePaths.history} element={<HistoryPage />} />
      <Route path={`${sessionPagePaths.historyPrefix}:sessionId`} element={<HistoryDetailRoute />} />
      <Route path={sessionPagePaths.statistics} element={<StatisticsPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Route>
  </Routes>
);

export default App;
