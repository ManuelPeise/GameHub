import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router";
import CssBaseline from "@mui/material/CssBaseline";
import { ThemeProvider } from "@mui/material/styles";
import { CurrentUserProvider } from "./app/CurrentUser.tsx";
import { theme } from "./app/theme.ts";
import App from "./pages/App.tsx";

createRoot(document.getElementById("root")!).render(
  <ThemeProvider theme={theme}>
    <CssBaseline enableColorScheme />
    <BrowserRouter>
      <CurrentUserProvider>
        <App />
      </CurrentUserProvider>
    </BrowserRouter>
  </ThemeProvider>,
);
