import React from "react";
import { sendRequest } from "../hooks/apiClient";
import { useStatefulApi } from "../hooks/useStatefulApi";
import { accountUrls, pagePaths, type CurrentUserResponse } from "../api/account";
import "./account/AccountPages.css";

/** Start page: shows the signed-in user, otherwise links to login and registration. */
const HomePage: React.FC = () => {
  const currentUser = useStatefulApi<CurrentUserResponse>({ serviceUrl: accountUrls.me });
  const loadCurrentUser = currentUser.get;

  React.useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  const logout = async () => {
    await sendRequest<null>("POST", { serviceUrl: accountUrls.logout });
    window.location.assign(pagePaths.login);
  };

  if (!currentUser.isDataBound && !currentUser.isError) {
    return <main className="account-page">Lädt …</main>;
  }

  return (
    <main className="account-page">
      <h1>GameHub</h1>
      {currentUser.data ? (
        <>
          <p>
            Angemeldet als <strong>{currentUser.data.displayName}</strong> ({currentUser.data.email})
          </p>
          <div className="account-form">
            <button type="button" onClick={() => void logout()}>
              Abmelden
            </button>
          </div>
        </>
      ) : (
        <p className="account-links">
          <a href={pagePaths.login}>Anmelden</a>
          <a href={pagePaths.register}>Registrieren</a>
        </p>
      )}
    </main>
  );
};

export default HomePage;
