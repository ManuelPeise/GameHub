import React from "react";
import { accountUrls, type CurrentUserResponse } from "../api/account";
import { sendRequest } from "../hooks/apiClient";

type CurrentUserContextValue = {
  /** Signed-in user; null when signed out; undefined while loading. */
  user: CurrentUserResponse | null | undefined;
  reload: () => Promise<void>;
  logout: () => Promise<void>;
};

const CurrentUserContext = React.createContext<CurrentUserContextValue | null>(
  null,
);

/** Loads the signed-in user once and shares it with layout and pages. */
export const CurrentUserProvider: React.FC<React.PropsWithChildren> = ({
  children,
}) => {
  const [user, setUser] = React.useState<
    CurrentUserResponse | null | undefined
  >(undefined);

  const reload = React.useCallback(async () => {
    try {
      setUser(
        await sendRequest<CurrentUserResponse>("GET", {
          serviceUrl: accountUrls.me,
        }),
      );
    } catch {
      setUser(null);
    }
  }, []);

  const logout = React.useCallback(async () => {
    try {
      await sendRequest<null>("POST", { serviceUrl: accountUrls.logout });
    } finally {
      setUser(null);
    }
  }, []);

  React.useEffect(() => {
    const onLoad = async () => {
      await reload();
    };
    void onLoad();
  }, [reload]);

  const value = React.useMemo(
    () => ({ user, reload, logout }),
    [user, reload, logout],
  );
  return (
    <CurrentUserContext.Provider value={value}>
      {children}
    </CurrentUserContext.Provider>
  );
};

// eslint-disable-next-line react-refresh/only-export-components
export const useCurrentUser = (): CurrentUserContextValue => {
  const context = React.useContext(CurrentUserContext);
  if (!context) {
    throw new Error("useCurrentUser must be used inside CurrentUserProvider.");
  }
  return context;
};
