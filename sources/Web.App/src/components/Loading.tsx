import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/** Centered spinner with an optional text. */
const Loading = ({ text = "Lädt …" }: { text?: string }) => (
  <Stack spacing={2} sx={{ alignItems: "center", py: 6 }} role="status">
    <CircularProgress />
    <Typography color="text.secondary">{text}</Typography>
  </Stack>
);

export default Loading;
