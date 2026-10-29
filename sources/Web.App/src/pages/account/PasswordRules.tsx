import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import CheckCircleOutline from "@mui/icons-material/CheckCircleOutline";

/** Password rules from the server as a compact list. */
const PasswordRules = ({ rules, id }: { rules: string[]; id?: string }) => (
  <List dense disablePadding id={id}>
    {rules.map((rule) => (
      <ListItem key={rule} disableGutters sx={{ py: 0 }}>
        <ListItemIcon sx={{ minWidth: 28 }}>
          <CheckCircleOutline fontSize="small" color="disabled" />
        </ListItemIcon>
        <ListItemText primary={rule} slotProps={{ primary: { variant: "body2", color: "text.secondary" } }} />
      </ListItem>
    ))}
  </List>
);

export default PasswordRules;
