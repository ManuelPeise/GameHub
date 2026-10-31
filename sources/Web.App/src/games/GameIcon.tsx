import React from "react";
import SvgIcon, { type SvgIconProps } from "@mui/material/SvgIcon";
import Casino from "@mui/icons-material/Casino";
import GridOn from "@mui/icons-material/GridOn";
import SportsEsportsOutlined from "@mui/icons-material/SportsEsportsOutlined";

/** Mühle icon: three nested squares with the connecting lines. */
export const MuehleIcon: React.FC<SvgIconProps> = (props) => (
  <SvgIcon {...props}>
    <g fill="none" stroke="currentColor" strokeWidth={1.6}>
      <rect x="2.5" y="2.5" width="19" height="19" rx="1" />
      <rect x="6.5" y="6.5" width="11" height="11" rx="0.5" />
      <rect x="10" y="10" width="4" height="4" />
      <path d="M12 2.5V10M12 14v7.5M2.5 12H10M14 12h7.5" />
    </g>
  </SvgIcon>
);

/** Icon of a game type (Tic-Tac-Toe, Kniffel, Mühle). */
const GameIcon: React.FC<SvgIconProps & { gameType: string }> = ({ gameType, ...props }) => {
  switch (gameType) {
    case "TicTacToe":
      return <GridOn {...props} />;
    case "Kniffel":
      return <Casino {...props} />;
    case "Muehle":
      return <MuehleIcon {...props} />;
    default:
      return <SportsEsportsOutlined {...props} />;
  }
};

export default GameIcon;
