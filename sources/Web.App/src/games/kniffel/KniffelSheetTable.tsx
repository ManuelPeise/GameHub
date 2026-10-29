import React from "react";
import Chip from "@mui/material/Chip";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import type { SeatDto } from "../../api/sessions";
import {
  categories,
  categoryLabels,
  legalCategories,
  lowerSum,
  pointsFor,
  total,
  upperBonus,
  upperSum,
  type KniffelState,
} from "./kniffel";

type KniffelSheetTableProps = {
  state: KniffelState;
  seats: SeatDto[];
  mySeatId?: string | null;
  /** Set when the viewer may score now; open legal categories become buttons with their points. */
  onScore?: (categoryIndex: number) => void;
};

const stickyCell = { position: "sticky", left: 0, bgcolor: "background.paper", zIndex: 1 } as const;

/** Score sheets of all seats side by side; scrolls horizontally on small screens with a fixed first column. */
const KniffelSheetTable: React.FC<KniffelSheetTableProps> = ({ state, seats, mySeatId, onScore }) => {
  const ordered = [...seats].sort((a, b) => a.order - b.order);
  const mySeat = ordered.find((seat) => seat.id === mySeatId);
  const mySheet = mySeat ? state.sheets[mySeat.order] : undefined;
  const legal = onScore && mySheet ? legalCategories(mySheet, state.dice) : [];

  const sumRow = (label: string, value: (order: number) => number, strong = false) => (
    <TableRow sx={{ "& td, & th": { fontWeight: strong ? 800 : 600, bgcolor: strong ? "rgba(157, 123, 255, 0.08)" : undefined } }}>
      <TableCell component="th" scope="row" sx={stickyCell}>
        {label}
      </TableCell>
      {ordered.map((seat) => (
        <TableCell key={seat.id} align="right">
          {value(seat.order)}
        </TableCell>
      ))}
    </TableRow>
  );

  return (
    <TableContainer component={Paper} variant="outlined" sx={{ maxWidth: "100%" }}>
      <Table size="small" aria-label="Wertungsblöcke" sx={{ "& td, & th": { px: { xs: 1, sm: 1.5 }, whiteSpace: "nowrap" } }}>
        <TableHead>
          <TableRow>
            <TableCell sx={stickyCell}>Kategorie</TableCell>
            {ordered.map((seat) => {
              const isCurrent = seat.order === state.currentSeat && state.status === "Running";
              return (
                <TableCell
                  key={seat.id}
                  align="right"
                  sx={{
                    fontWeight: 700,
                    color: isCurrent ? "primary.main" : undefined,
                    textDecoration: state.removed[seat.order] ? "line-through" : undefined,
                    opacity: state.removed[seat.order] ? 0.6 : 1,
                  }}
                >
                  {seat.id === mySeatId ? "Du" : seat.displayName}
                </TableCell>
              );
            })}
          </TableRow>
        </TableHead>
        <TableBody>
          {categories.map((category, index) => (
            <React.Fragment key={category}>
              <TableRow hover>
                <TableCell component="th" scope="row" sx={stickyCell}>
                  {categoryLabels[category]}
                </TableCell>
                {ordered.map((seat) => {
                  const score = state.sheets[seat.order]?.scores[index];
                  const canScore = seat.id === mySeatId && legal.includes(index) && onScore && mySheet;
                  return (
                    <TableCell key={seat.id} align="right">
                      {score !== null && score !== undefined ? (
                        score
                      ) : canScore ? (
                        <Chip
                          size="small"
                          color={pointsFor(mySheet, state.dice, index) > 0 ? "primary" : "default"}
                          variant="outlined"
                          clickable
                          onClick={() => onScore(index)}
                          label={pointsFor(mySheet, state.dice, index)}
                          aria-label={`${categoryLabels[category]} werten: ${pointsFor(mySheet, state.dice, index)} Punkte`}
                          sx={{ minWidth: 44, fontWeight: 700 }}
                        />
                      ) : (
                        ""
                      )}
                    </TableCell>
                  );
                })}
              </TableRow>
              {index === 5 && (
                <>
                  {sumRow("Summe oben", (order) => upperSum(state.sheets[order]))}
                  {sumRow("Bonus (ab 63)", (order) => upperBonus(state.sheets[order]))}
                </>
              )}
            </React.Fragment>
          ))}
          {sumRow("Summe unten", (order) => lowerSum(state.sheets[order]))}
          {sumRow("Kniffel-Bonus", (order) => state.sheets[order].kniffelBonus)}
          {sumRow("Gesamt", (order) => total(state.sheets[order]), true)}
        </TableBody>
      </Table>
    </TableContainer>
  );
};

export default KniffelSheetTable;
