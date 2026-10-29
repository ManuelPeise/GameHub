import React from "react";
import { sendRequest } from "../hooks/apiClient";
import "./EnvironmentBanner.css";

type ApiInfo = {
  environment: string;
  showBanner: boolean;
};

const labels: Record<string, string> = {
  Development: "Entwicklung",
  Testing: "Testumgebung",
};

/** Shows outside of Live which environment this is, so test data is not mistaken for real games. */
const EnvironmentBanner: React.FC = () => {
  const [info, setInfo] = React.useState<ApiInfo | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    sendRequest<ApiInfo>("GET", { serviceUrl: "/api/info" })
      .then((loaded) => {
        if (!cancelled) {
          setInfo(loaded);
        }
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  if (!info?.showBanner) {
    return null;
  }

  return (
    <div className={`environment-banner environment-banner--${info.environment.toLowerCase()}`} role="note">
      {labels[info.environment] ?? info.environment} – keine echten Daten
    </div>
  );
};

export default EnvironmentBanner;
