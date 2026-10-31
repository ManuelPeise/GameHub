/** Mühle board geometry and rule helpers for the UI (the server checks every move again). */

export type MuehleMove = { from?: number | null; to: number; remove?: number | null };

export type MuehleState = {
  board: string;
  nextSeat: number;
  inHand: number[];
  status: "Running" | "Won" | "Draw";
  winnerSeat?: number | null;
  endReason?: "TooFewStones" | "Blocked" | "Repetition" | "NoCapture" | null;
  pliesWithoutCapture: number;
  lastMove?: MuehleMove | null;
};

export type MuehlePhase = "Placing" | "Moving" | "Flying";

export const pointCount = 24;
export const emptyPoint = "-";

/** Point coordinates on a 7×7 grid: three squares, each clockwise from the top left corner. */
export const pointPositions: { x: number; y: number }[] = [0, 1, 2].flatMap((ring) => {
  const low = ring;
  const high = 6 - ring;
  return [
    { x: low, y: low },
    { x: 3, y: low },
    { x: high, y: low },
    { x: high, y: 3 },
    { x: high, y: high },
    { x: 3, y: high },
    { x: low, y: high },
    { x: low, y: 3 },
  ];
});

export const neighbours: number[][] = (() => {
  const result: number[][] = Array.from({ length: pointCount }, () => []);
  const link = (a: number, b: number) => {
    result[a].push(b);
    result[b].push(a);
  };
  for (let ring = 0; ring < 3; ring++) {
    for (let index = 0; index < 8; index++) {
      const point = ring * 8 + index;
      link(point, ring * 8 + ((index + 1) % 8));
      if (index % 2 === 1 && ring < 2) {
        link(point, point + 8);
      }
    }
  }
  return result;
})();

export const mills: number[][] = [
  ...[0, 1, 2].flatMap((ring) => [0, 2, 4, 6].map((corner) => [ring * 8 + corner, ring * 8 + corner + 1, ring * 8 + ((corner + 2) % 8)])),
  ...[1, 3, 5, 7].map((middle) => [middle, middle + 8, middle + 16]),
];

export const colorOf = (seat: number) => (seat === 0 ? "W" : "B");
export const colorName = (seat: number) => (seat === 0 ? "Weiß" : "Schwarz");

export const parseMuehleState = (json: string): MuehleState | null => {
  try {
    const state = JSON.parse(json) as MuehleState;
    return typeof state.board === "string" && state.board.length === pointCount && Array.isArray(state.inHand) ? state : null;
  } catch {
    return null;
  }
};

export const stonesOnBoard = (board: string, seat: number) => [...board].filter((point) => point === colorOf(seat)).length;

export const phaseOf = (state: MuehleState, seat: number): MuehlePhase =>
  state.inHand[seat] > 0 ? "Placing" : stonesOnBoard(state.board, seat) === 3 ? "Flying" : "Moving";

export const phaseLabels: Record<MuehlePhase, string> = { Placing: "Setzen", Moving: "Ziehen", Flying: "Springen" };

/** Board after moving (or placing) a stone, without a capture. */
export const applyStep = (board: string, from: number | null, to: number, color: string) => {
  const cells = [...board];
  if (from !== null) {
    cells[from] = emptyPoint;
  }
  cells[to] = color;
  return cells.join("");
};

export const isInMill = (board: string, point: number) =>
  board[point] !== emptyPoint && mills.some((mill) => mill.includes(point) && mill.every((p) => board[p] === board[point]));

/** Opponent stones that may be taken: those outside mills, or all if every stone is in a mill. */
export const removableStones = (board: string, ofSeat: number) => {
  const stones = [...board].flatMap((point, index) => (point === colorOf(ofSeat) ? [index] : []));
  const free = stones.filter((point) => !isInMill(board, point));
  return free.length > 0 ? free : stones;
};

/** Targets for a stone (or for placing when from is null). */
export const targetsOf = (state: MuehleState, seat: number, from: number | null) => {
  const empty = [...state.board].flatMap((point, index) => (point === emptyPoint ? [index] : []));
  const phase = phaseOf(state, seat);
  if (phase === "Placing") {
    return from === null ? empty : [];
  }
  if (from === null || state.board[from] !== colorOf(seat)) {
    return [];
  }
  return phase === "Flying" ? empty : neighbours[from].filter((point) => state.board[point] === emptyPoint);
};

/** Mills that are complete on the board (for highlighting). */
export const closedMills = (board: string) => mills.filter((mill) => board[mill[0]] !== emptyPoint && mill.every((p) => board[p] === board[mill[0]]));

export const endReasonLabels: Record<string, string> = {
  TooFewStones: "weniger als drei Steine",
  Blocked: "kein Zug mehr möglich",
  Repetition: "dreimal dieselbe Stellung",
  NoCapture: "50 Züge ohne Schlagen",
};
