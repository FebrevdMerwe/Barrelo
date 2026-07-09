/**
 * The wire contract Barrelo speaks to an out-of-process game over HTTP/JSON, mirrored 1:1 from the
 * C# records in src/Barrelo.GameSdk (see that project for the source of truth). Barrelo serializes with
 * camelCase property names and string enums (Barrelo.Infrastructure/External/GamePlugins/RemoteGameJsonOptions.cs),
 * which is what these shapes assume. Imported by both src/server.ts and ui/src — keep it the one place
 * this contract is defined so the two sides can't drift apart.
 */

export type Ring = "Miss" | "Inner" | "Outer" | "Triple" | "Double" | "InnerBull" | "OuterBull";

export type GameStatus = "InProgress" | "Complete" | "Aborted";

export type DetectionSourceType = "ThirdPartyDetector" | "Mock" | "Manual" | "Simulator";

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

/** POST /create body — the only time Barrelo tells the process who's playing. */
export interface GameSetup {
  playerIds: string[];
  options: Record<string, string>;
  playerGroups: Record<string, number> | null;
}

/**
 * The universal state envelope returned by GET /state. Game-specific shape lives entirely in `payload` —
 * never add game-specific fields alongside it, matching the same rule the in-process GameStateSnapshot
 * envelope follows.
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
  payload: unknown;
}

/** GET /result body — only meaningful once GameStateSnapshot.isComplete is true. */
export interface GameResult {
  winnerPlayerIds: string[];
  finalStandings: string[];
}

/** The message the host page posts into this game's ui/index.html iframe on every state push. */
export interface BarreloGameStateMessage {
  type: "barrelo:gameState";
  snapshot: GameStateSnapshot;
  playerNames: Record<string, string>;
}
