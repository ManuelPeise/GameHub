import React from "react";
import Box from "@mui/material/Box";
import { useTheme } from "@mui/material/styles";
import { closedMills, emptyPoint, pointPositions } from "./muehle";

export type MuehleBoardProps = {
  board: string;
  /** Points the player can tap now. */
  clickable?: number[];
  /** Possible targets (shown as dots). */
  targets?: number[];
  /** Stones that may be taken (red ring). */
  removable?: number[];
  selected?: number | null;
  lastMove?: { from?: number | null; to: number; remove?: number | null } | null;
  onPoint?: (point: number) => void;
};

const ringLines = [0, 1, 2].map((ring) => ({ start: ring, size: 6 - 2 * ring }));
const crossLines = [
  [3, 0, 3, 2],
  [3, 4, 3, 6],
  [0, 3, 2, 3],
  [4, 3, 6, 3],
];

/** The Mühle board as SVG: fixed square size, fills the phone width; large tap areas around every point. */
const MuehleBoard: React.FC<MuehleBoardProps> = ({ board, clickable = [], targets = [], removable = [], selected = null, lastMove, onPoint }) => {
  const theme = useTheme();
  const millPoints = new Set(closedMills(board).flat());
  const line = theme.palette.text.primary;

  return (
    <Box
      component="svg"
      viewBox="-0.7 -0.7 7.4 7.4"
      role="grid"
      aria-label="Mühle-Spielbrett"
      sx={{ display: "block", width: "100%", maxWidth: 440, mx: "auto", aspectRatio: "1", bgcolor: "#f6d9a8", borderRadius: 4, border: 3, borderColor: "#c8935a", boxShadow: "0 6px 0 rgba(43, 34, 80, 0.12)", touchAction: "manipulation" }}
    >
      <g stroke={line} strokeWidth={0.07} fill="none" strokeLinecap="round" opacity={0.75}>
        {ringLines.map((ring) => (
          <rect key={ring.start} x={ring.start} y={ring.start} width={ring.size} height={ring.size} />
        ))}
        {crossLines.map(([x1, y1, x2, y2]) => (
          <line key={`${x1}-${y1}`} x1={x1} y1={y1} x2={x2} y2={y2} />
        ))}
      </g>

      {pointPositions.map((position, point) => {
        const stone = board[point];
        const isClickable = clickable.includes(point) && onPoint !== undefined;
        const isTarget = targets.includes(point);
        const isRemovable = removable.includes(point);
        const isSelected = selected === point;
        const isLast = lastMove?.to === point;
        const label = `Punkt ${point + 1}: ${stone === "W" ? "weißer Stein" : stone === "B" ? "schwarzer Stein" : "frei"}`;

        return (
          <g
            key={point}
            role="gridcell"
            aria-label={label}
            tabIndex={isClickable ? 0 : -1}
            onClick={isClickable ? () => onPoint?.(point) : undefined}
            onKeyDown={isClickable ? (event) => (event.key === "Enter" || event.key === " ") && onPoint?.(point) : undefined}
            style={{ cursor: isClickable ? "pointer" : "default", outline: "none" }}
          >
            <circle cx={position.x} cy={position.y} r={0.5} fill="transparent" />
            {stone === emptyPoint ? (
              <circle cx={position.x} cy={position.y} r={isTarget ? 0.18 : 0.11} fill={isTarget ? theme.palette.success.main : line} opacity={isTarget ? 0.9 : 0.75} />
            ) : (
              <>
                {(isSelected || isRemovable || millPoints.has(point)) && (
                  <circle
                    cx={position.x}
                    cy={position.y}
                    r={0.47}
                    fill="none"
                    stroke={isRemovable ? theme.palette.error.main : isSelected ? theme.palette.secondary.main : theme.palette.primary.main}
                    strokeWidth={isRemovable || isSelected ? 0.09 : 0.05}
                    strokeDasharray={isRemovable ? "0.15 0.08" : undefined}
                  />
                )}
                <circle
                  cx={position.x}
                  cy={position.y + 0.05}
                  r={0.36}
                  fill={stone === "W" ? "#fffdf7" : "#2b2250"}
                  stroke={stone === "W" ? "#8a7a99" : "#120c2b"}
                  strokeWidth={0.05}
                />
                <circle cx={position.x - 0.1} cy={position.y - 0.08} r={0.1} fill={stone === "W" ? "#ffffff" : "#5b4f8a"} opacity={0.8} />
                {isLast && <circle cx={position.x} cy={position.y + 0.05} r={0.1} fill={theme.palette.secondary.main} />}
              </>
            )}
          </g>
        );
      })}

      {lastMove?.remove !== undefined && lastMove?.remove !== null && (
        <g opacity={0.6} aria-hidden>
          <line x1={pointPositions[lastMove.remove].x - 0.2} y1={pointPositions[lastMove.remove].y - 0.2} x2={pointPositions[lastMove.remove].x + 0.2} y2={pointPositions[lastMove.remove].y + 0.2} stroke={theme.palette.error.main} strokeWidth={0.07} strokeLinecap="round" />
          <line x1={pointPositions[lastMove.remove].x + 0.2} y1={pointPositions[lastMove.remove].y - 0.2} x2={pointPositions[lastMove.remove].x - 0.2} y2={pointPositions[lastMove.remove].y + 0.2} stroke={theme.palette.error.main} strokeWidth={0.07} strokeLinecap="round" />
        </g>
      )}
    </Box>
  );
};

export default MuehleBoard;
