import { createTheme } from "@mui/material/styles";
import LinkBehavior from "./LinkBehavior";

/** Dark Material theme of GameHub. Links and link buttons navigate through React Router. */
export const theme = createTheme({
  palette: {
    mode: "dark",
    primary: { main: "#9d7bff" },
    secondary: { main: "#ffb74d" },
    success: { main: "#66bb6a" },
    background: { default: "#0f1117", paper: "#171a23" },
    divider: "rgba(255, 255, 255, 0.08)",
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", system-ui, -apple-system, Roboto, sans-serif',
    h1: { fontSize: "2rem", fontWeight: 700 },
    h2: { fontSize: "1.5rem", fontWeight: 700 },
    h3: { fontSize: "1.2rem", fontWeight: 600 },
    button: { textTransform: "none", fontWeight: 600 },
  },
  components: {
    MuiLink: { defaultProps: { component: LinkBehavior } },
    MuiButtonBase: { defaultProps: { LinkComponent: LinkBehavior } },
    MuiButton: { defaultProps: { disableElevation: true } },
    MuiCard: { defaultProps: { variant: "outlined" } },
    MuiPaper: { styleOverrides: { root: { backgroundImage: "none" } } },
    MuiTextField: { defaultProps: { fullWidth: true } },
    MuiAppBar: { styleOverrides: { root: { backgroundImage: "none" } } },
  },
});
