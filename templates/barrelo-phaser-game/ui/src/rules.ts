import type { ClientGamePayload, DetectedThrow, Visit } from "../../shared/types";

/**
 * TODO: this is the only file your game's rules live in. Replace the state shape and the fold below;
 * leave the "pure function of the payload" property alone, because everything else depends on it.
 *
 * WHY IT MUST BE PURE
 * -------------------
 * Barrelo keeps the log; every screen showing this match (the control tablet, the TV) replays it
 * independently. They only agree if replaying the same log always produces the same state. So:
 *
 *   - No `Math.random()` — use `rng(payload.seed)` below.
 *   - No `Date.now()` / `new Date()` — use `throw.detectedAtUtc` from the log.
 *   - No `crypto.randomUUID()` — derive ids from the log (e.g. `throw.throwId`).
 *   - No reading or writing anything outside this function (no module-level mutable state, no
 *     localStorage, no fetch).
 *
 * Break any of those and the TV quietly shows something different from the tablet. Barrelo detects this
 * — every screen reports a hash of its derived state and the host logs a warning when two disagree —
 * but the fix is always here.
 */

export interface GameState {
  /** Whose turn it is. Barrelo can't know this — turn order is a rule — so the board reports it back up. */
  currentPlayerId: string | null;
  /** Darts thrown in the visit currently in progress, for the shell's 1/2/3 slots. */
  currentVisitThrows: DetectedThrow[];
  /** TODO: replace with whatever your game tracks — score, marks, lives, positions... */
  scoreByPlayer: Record<string, number>;
  winnerPlayerIds: string[];
  /** Best-first ranking, reported to Barrelo when the match ends so it can award leaderboard points. */
  finalStandings: string[];
  isComplete: boolean;
}

/**
 * A tiny deterministic PRNG (mulberry32), seeded from the payload. Call it for anything random — the same
 * seed reaches every screen, so they all draw the same sequence.
 */
export function rng(seed: number): () => number {
  let a = seed >>> 0;
  return function next(): number {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** A visit is over when the turn boundary arrived, or when three darts have been thrown. */
function isVisitOver(visit: Visit): boolean {
  return visit.ended || visit.throws.length >= 3;
}

/**
 * TODO: your rules. This template just counts each player's score and rotates turns in player order,
 * never finishing — enough to prove the pipeline works, not enough to be a game.
 */
export function replay(payload: ClientGamePayload): GameState {
  // Defaulted rather than destructured straight out: a board can be rendered before the first real
  // snapshot arrives (and the dev harness starts with an empty match), and a crash here is a blank
  // screen with a stack trace in a sandboxed iframe nobody is watching.
  const playerIds = payload?.playerIds ?? [];
  const visits = payload?.visits ?? [];

  const scoreByPlayer: Record<string, number> = {};
  playerIds.forEach((id) => {
    scoreByPlayer[id] = 0;
  });

  if (playerIds.length === 0) {
    return {
      currentPlayerId: null,
      currentVisitThrows: [],
      scoreByPlayer,
      winnerPlayerIds: [],
      finalStandings: [],
      isComplete: false,
    };
  }

  let turnIndex = 0;
  let currentVisitThrows: DetectedThrow[] = [];

  visits.forEach((visit) => {
    const playerId = playerIds[turnIndex];
    visit.throws.forEach((t) => {
      scoreByPlayer[playerId] += t.score;
    });

    if (isVisitOver(visit)) {
      // TODO: whose turn is next is a rules decision — an elimination game skips dead players, and a
      // "hit a bull, throw again" game doesn't advance at all. Barrelo deliberately doesn't assume.
      turnIndex = (turnIndex + 1) % playerIds.length;
      currentVisitThrows = [];
    } else {
      currentVisitThrows = visit.throws;
    }
  });

  // TODO: decide your win condition here, then populate winnerPlayerIds and finalStandings. Until
  // isComplete goes true the match never ends and no leaderboard points are awarded.
  const winnerPlayerIds: string[] = [];

  return {
    currentPlayerId: playerIds[turnIndex],
    currentVisitThrows,
    scoreByPlayer,
    winnerPlayerIds,
    finalStandings: winnerPlayerIds.length > 0 ? [...playerIds] : [],
    isComplete: winnerPlayerIds.length > 0,
  };
}

function hash(canonical: string): string {
  let value = 0;
  for (let i = 0; i < canonical.length; i++) {
    value = (Math.imul(31, value) + canonical.charCodeAt(i)) | 0;
  }
  return (value >>> 0).toString(16);
}

/**
 * Identifies a derived state so two screens can be compared. A cheap string hash over the fields you
 * actually render is plenty — it only has to change whenever the state changes.
 */
export function hashState(state: GameState): string {
  return hash(
    JSON.stringify([state.currentPlayerId, state.scoreByPlayer, state.winnerPlayerIds, state.isComplete])
  );
}

/**
 * Identifies the log a state was derived *from*. Barrelo compares screens by pairing this with
 * hashState: two screens observing the match a moment apart hold different logs and aren't compared,
 * so only a genuine same-input/different-output disagreement is reported.
 *
 * Hashed over each dart's identity and the visit boundaries — the whole of the input replay() reads.
 */
export function hashLog(payload: ClientGamePayload): string {
  return hash(
    JSON.stringify(
      (payload?.visits ?? []).map((visit) => [visit.throws.map((t) => t.throwId), visit.ended])
    )
  );
}
