import React from "react";
import { pagePaths } from "../api/account";
import EmailConfirmedPage from "./account/EmailConfirmedPage";
import ForgotPasswordPage from "./account/ForgotPasswordPage";
import LoginPage from "./account/LoginPage";
import RegisterPage from "./account/RegisterPage";
import ResetPasswordPage from "./account/ResetPasswordPage";
import AdminPage from "./admin/AdminPage";
import HomePage from "./HomePage";

/** Minimal path-based page selection until routing and layout arrive. */
const App: React.FC = () => {
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
    case pagePaths.admin:
      return <AdminPage />;
    default:
      return <HomePage />;
  }
};

export default App;
