import { createRoot } from "react-dom/client";

import EnvironmentBanner from "./components/EnvironmentBanner.tsx";
import App from "./pages/App.tsx";

createRoot(document.getElementById("root")!).render(
  <>
    <EnvironmentBanner />
    <App />
  </>,
);
