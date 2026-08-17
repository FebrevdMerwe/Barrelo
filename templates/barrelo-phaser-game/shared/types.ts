/**
 * The contract Barrelo speaks to a client-owned game, mirrored 1:1 from the C# records in
 * src/Barrelo.GameSdk and src/Barrelo.Infrastructure/External/GamePlugins/ClientOwnedGame.cs (see those
 * for the source of truth). Barrelo serializes with camelCase property names and string enums, which is
 * what these shapes assume.
 *
 * Everything crosses a single postMessage channel between Barrelo's page and this game's iframe — there
 * is no HTTP contract and no server half. Barrelo pushes state down; the game sends display hints and its
 * final result back up.
 */

export type Ring = "Miss" | "InnerSingle" | "OuterSingle" | "Triple" | "Double" | "Single";

export type GameStatus = "InProgress" | "Complete" | "Aborted";

export type DetectionSourceType = "AutoDarts" | "Mock" | "Manual" | "Simulator";

export interface BoardPosition {
  x: number;
  y: number;
}

export interface DetectedThrow {
  throwId: string;
  segment: number;
  ring: Ring;
  score: number;
  rawNotation: string;
  position: BoardPosition;
  confidence: number | null;
  boardId: string;
  cameraIndex: number | null;
  detectedAtUtc: string;
  source: DetectionSourceType;
}

/** One player's turn at the board. Open (`ended: false`) until the turn boundary arrives; only the last
 *  visit in the log can be open. */
export interface Visit {
  throws: DetectedThrow[];
  ended: boolean;
}

/**
 * Everything Barrelo knows about the match — which is deliberately only who's playing, how it was set up,
 * and what has been thrown. Your rules derive everything else from this by replaying `visits`.
 *
 * `seed` is fixed for the match and identical on every screen. It is the *only* source of randomness a
 * game may use: see the determinism contract in the README.
 */
export interface ClientGamePayload {
  seed: number;
  /** The roster in turn order. Barrelo interleaves team members here (A[0], B[0], A[1], B[1], ...). */
  playerIds: string[];
  options: Record<string, string>;
  /**
   * Player id → team index, from the team assignment the start screen collected. Teams are Barrelo's
   * default unit and this is how you read them: solo play is the same shape with one player per index.
   *
   * A player missing from this map (or an empty map, when the match was started without the team setting)
   * falls back to their own position in `playerIds` — an implicit team of one. Resolve it with
   * `effectiveGroupIndex()` in `ui/src/rules.ts` rather than indexing directly, so that fallback is
   * applied consistently; it mirrors the host's `GameSetupExtensions.EffectiveGroupIndex`.
   */
  playerGroups: Record<string, number>;
  visits: Visit[];
}

/**
 * The universal state envelope. For a client-owned game the host fills in only what it can actually
 * know: `currentPlayerId` is always null and `legNumber`/`setNumber` are always 1, because those are
 * rules output. Read them from your own replay, not from here.
 */
export interface GameStateSnapshot {
  matchId: string;
  gameId: string;
  status: GameStatus;
  currentPlayerId: string | null;
  legNumber: number;
  setNumber: number;
  recentThrows: DetectedThrow[];
  isComplete: boolean;
  winnerPlayerIds: string[] | null;
  payload: ClientGamePayload;
}

/** Pushed into this game's iframe on every state change. */
export interface BarreloGameStateMessage {
  type: "barrelo:gameState";
  snapshot: GameStateSnapshot;
  /** Every player Barrelo knows about, not just this match's — look up by id from `payload.playerIds`. */
  playerNames: Record<string, string>;
}

/**
 * Sent up to Barrelo to drive the chrome around the board: the turn indicator, the dart 1/2/3 slots, the
 * leg/set label, and which dartboard targets to grey out. All fields are optional; anything omitted falls
 * back to what the host already shows. Advisory only — none of it is trusted for scoring.
 *
 * `logHash`/`stateHash` are how Barrelo detects a non-deterministic replay: every screen reports which
 * log it replayed and what it derived, and Barrelo warns if the same log produced different states.
 */
export interface BarreloDisplayMessage {
  type: "barrelo:display";
  currentPlayerId?: string | null;
  legNumber?: number;
  setNumber?: number;
  visitThrows?: DetectedThrow[];
  /** Segment numbers to grey out on the input dartboard, plus "BULL" for the bullseye. */
  deadTargets?: (number | "BULL")[];
  /** Identifies the log that was replayed — must be paired with `stateHash`. */
  logHash?: string;
  stateHash?: string;
}

/**
 * Sent up to Barrelo exactly once, when your rules say the match is over. Barrelo ends the session and
 * awards session-leaderboard points from `finalStandings` (best first). Both lists are validated against
 * the match roster and rejected if they don't match.
 *
 * Both are lists of *player* ids even though your rules rank teams: a winning team contributes every one
 * of its members to `winnerPlayerIds`, and `finalStandings` lists each team's members together, best team
 * first. That's how every member of a winning team gets their leaderboard points.
 */
export interface BarreloMatchCompleteMessage {
  type: "barrelo:matchComplete";
  winnerPlayerIds: string[];
  finalStandings: string[];
}
