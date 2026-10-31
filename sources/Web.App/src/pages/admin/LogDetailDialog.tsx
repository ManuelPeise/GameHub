import React from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useTheme } from "@mui/material/styles";

export type DetailField = { label: string; value?: React.ReactNode; code?: boolean };

/** Details of a log entry; full screen on phones. */
const LogDetailDialog: React.FC<{ title: string; fields: DetailField[] | null; onClose: () => void }> = ({ title, fields, onClose }) => {
  const theme = useTheme();
  const fullScreen = useMediaQuery(theme.breakpoints.down("sm"));

  return (
    <Dialog open={fields !== null} onClose={onClose} fullScreen={fullScreen} fullWidth maxWidth="md">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {fields
            ?.filter((field) => field.value !== undefined && field.value !== null && field.value !== "")
            .map((field) => (
              <Box key={field.label}>
                <Typography variant="caption" color="text.secondary">
                  {field.label}
                </Typography>
                {field.code ? (
                  <Box
                    component="pre"
                    sx={{ m: 0, p: 1.5, bgcolor: "background.default", borderRadius: 2, overflowX: "auto", fontSize: "0.8rem", whiteSpace: "pre-wrap", wordBreak: "break-word" }}
                  >
                    {field.value}
                  </Box>
                ) : (
                  <Typography sx={{ wordBreak: "break-word" }}>{field.value}</Typography>
                )}
              </Box>
            ))}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Schließen</Button>
      </DialogActions>
    </Dialog>
  );
};

export default LogDetailDialog;
