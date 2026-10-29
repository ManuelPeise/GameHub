import React from "react";

type TurnTimerProps = {
  /** Deadline of the seat at turn (ISO timestamp from the server); nothing is shown without one. */
  deadline?: string | null;
};

const format = (milliseconds: number): string => {
  const totalSeconds = Math.max(0, Math.ceil(milliseconds / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
};

/** Remaining time of the player at turn (multiplayer turn time limit). */
const TurnTimer: React.FC<TurnTimerProps> = ({ deadline }) => {
  const [now, setNow] = React.useState(() => Date.now());

  React.useEffect(() => {
    if (!deadline) {
      return;
    }
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [deadline]);

  if (!deadline) {
    return null;
  }

  const remaining = new Date(deadline).getTime() - now;
  return (
    <span className={remaining <= 30_000 ? "account-error" : undefined} aria-live="polite">
      noch {format(remaining)}
    </span>
  );
};

export default TurnTimer;
