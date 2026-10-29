/**
 * Kniffel state as stored by the server and scoring helpers for the preview in the score sheet.
 * The server validates every action; these functions only show what a category would bring.
 */

export const categories = [
  "Ones",
  "Twos",
  "Threes",
  "Fours",
  "Fives",
  "Sixes",
  "ThreeOfAKind",
  "FourOfAKind",
  "FullHouse",
  "SmallStraight",
  "LargeStraight",
  "Kniffel",
  "Chance",
] as const;

export type KniffelCategory = (typeof categories)[number];

export const categoryLabels: Record<KniffelCategory, string> = {
  Ones: "Einser",
  Twos: "Zweier",
  Threes: "Dreier",
  Fours: "Vierer",
  Fives: "Fünfer",
  Sixes: "Sechser",
  ThreeOfAKind: "Dreierpasch",
  FourOfAKind: "Viererpasch",
  FullHouse: "Full House",
  SmallStraight: "Kleine Straße",
  LargeStraight: "Große Straße",
  Kniffel: "Kniffel",
  Chance: "Chance",
};

export type KniffelSheet = {
  scores: (number | null)[];
  kniffelBonus: number;
};

export type KniffelState = {
  sheets: KniffelSheet[];
  removed: boolean[];
  currentSeat: number;
  rollsUsed: number;
  dice: number[];
  heldMask: number;
  status: "Running" | "Finished";
};

export const maxRolls = 3;

export const parseKniffelState = (json: string): KniffelState | null => {
  try {
    const state = JSON.parse(json) as KniffelState;
    return Array.isArray(state.sheets) && Array.isArray(state.dice) ? state : null;
  } catch {
    return null;
  }
};

const isUpper = (index: number) => index < 6;
const counts = (dice: number[]) => dice.reduce((result, die) => ((result[die] += 1), result), [0, 0, 0, 0, 0, 0, 0]);
const hasRun = (c: number[], length: number) => {
  let run = 0;
  for (let face = 1; face <= 6; face += 1) {
    run = c[face] > 0 ? run + 1 : 0;
    if (run >= length) {
      return true;
    }
  }
  return false;
};

export const isKniffel = (dice: number[]) => dice.length === 5 && dice[0] > 0 && dice.every((die) => die === dice[0]);

const rawScore = (dice: number[], index: number): number => {
  const c = counts(dice);
  const sum = dice.reduce((a, b) => a + b, 0);
  const max = Math.max(...c);
  switch (categories[index]) {
    case "ThreeOfAKind":
      return max >= 3 ? sum : 0;
    case "FourOfAKind":
      return max >= 4 ? sum : 0;
    case "FullHouse":
      return c.includes(3) && c.includes(2) ? 25 : 0;
    case "SmallStraight":
      return hasRun(c, 4) ? 30 : 0;
    case "LargeStraight":
      return hasRun(c, 5) ? 40 : 0;
    case "Kniffel":
      return max === 5 ? 50 : 0;
    case "Chance":
      return sum;
    default:
      return c[index + 1] * (index + 1);
  }
};

const kniffelIndex = categories.indexOf("Kniffel");

const isJoker = (sheet: KniffelSheet, dice: number[]) => isKniffel(dice) && sheet.scores[kniffelIndex] !== null;

/** Categories the dice may be scored in (official joker order for a further Kniffel). */
export const legalCategories = (sheet: KniffelSheet, dice: number[]): number[] => {
  const open = categories.map((_, index) => index).filter((index) => sheet.scores[index] === null);
  if (!isJoker(sheet, dice)) {
    return open;
  }
  const matching = dice[0] - 1;
  if (open.includes(matching)) {
    return [matching];
  }
  const lower = open.filter((index) => !isUpper(index));
  return lower.length > 0 ? lower : open;
};

/** Points incl. joker and Kniffel bonus – what scoring the category would add to the total. */
export const pointsFor = (sheet: KniffelSheet, dice: number[], index: number): number => {
  let points = rawScore(dice, index);
  if (isJoker(sheet, dice) && !isUpper(index)) {
    const joker: Partial<Record<KniffelCategory, number>> = { FullHouse: 25, SmallStraight: 30, LargeStraight: 40 };
    points = joker[categories[index]] ?? points;
  }
  const bonus = isKniffel(dice) && sheet.scores[kniffelIndex] === 50 ? 50 : 0;
  return points + bonus;
};

export const upperSum = (sheet: KniffelSheet) => sheet.scores.slice(0, 6).reduce<number>((a, b) => a + (b ?? 0), 0);
export const upperBonus = (sheet: KniffelSheet) => (upperSum(sheet) >= 63 ? 35 : 0);
export const lowerSum = (sheet: KniffelSheet) => sheet.scores.slice(6).reduce<number>((a, b) => a + (b ?? 0), 0);
export const total = (sheet: KniffelSheet) => upperSum(sheet) + upperBonus(sheet) + lowerSum(sheet) + sheet.kniffelBonus;
