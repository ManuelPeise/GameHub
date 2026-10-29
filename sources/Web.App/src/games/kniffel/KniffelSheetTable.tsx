import React from "react";
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

/** Score sheets of all seats side by side. */
const KniffelSheetTable: React.FC<KniffelSheetTableProps> = ({ state, seats, mySeatId, onScore }) => {
  const ordered = [...seats].sort((a, b) => a.order - b.order);
  const mySeat = ordered.find((seat) => seat.id === mySeatId);
  const mySheet = mySeat ? state.sheets[mySeat.order] : undefined;
  const legal = onScore && mySheet ? legalCategories(mySheet, state.dice) : [];

  const sumRow = (label: string, value: (order: number) => number) => (
    <tr className="kniffel-sum">
      <th scope="row">{label}</th>
      {ordered.map((seat) => (
        <td key={seat.id}>{value(seat.order)}</td>
      ))}
    </tr>
  );

  return (
    <table className="kniffel-sheet">
      <thead>
        <tr>
          <th scope="col">Kategorie</th>
          {ordered.map((seat) => (
            <th
              key={seat.id}
              scope="col"
              className={[
                seat.order === state.currentSeat && state.status === "Running" ? "kniffel-current" : "",
                state.removed[seat.order] ? "kniffel-removed" : "",
              ].join(" ")}
            >
              {seat.id === mySeatId ? "Du" : seat.displayName}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {categories.map((category, index) => (
          <React.Fragment key={category}>
            <tr>
              <th scope="row">{categoryLabels[category]}</th>
              {ordered.map((seat) => {
                const score = state.sheets[seat.order]?.scores[index];
                const canScore = seat.id === mySeatId && legal.includes(index) && onScore && mySheet;
                return (
                  <td key={seat.id}>
                    {score !== null && score !== undefined ? (
                      score
                    ) : canScore ? (
                      <button
                        type="button"
                        className="kniffel-score-option"
                        onClick={() => onScore(index)}
                        aria-label={`${categoryLabels[category]} werten: ${pointsFor(mySheet, state.dice, index)} Punkte`}
                      >
                        {pointsFor(mySheet, state.dice, index)}
                      </button>
                    ) : (
                      ""
                    )}
                  </td>
                );
              })}
            </tr>
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
        {sumRow("Gesamt", (order) => total(state.sheets[order]))}
      </tbody>
    </table>
  );
};

export default KniffelSheetTable;
