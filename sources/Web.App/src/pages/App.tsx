import React from "react";
import EmailConfirmedPage from "./account/EmailConfirmedPage";
import RegisterPage from "./account/RegisterPage";

/**
 * Minimal path-based page selection until routing, layout and login arrive (US-006 and later).
 */
const App: React.FC = () => {
  switch (window.location.pathname) {
    case "/email-confirmed":
      return <EmailConfirmedPage />;
    default:
      return <RegisterPage />;
  }
};

export default App;
