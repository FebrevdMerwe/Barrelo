import type { ClientGamePayload, DetectedThrow, Visit } from "../../shared/types";

/**
 * TODO: this is the only file your game's rules live in. Replace the state shape and the fold below;
 * leave the "pure function of the payload" property alone, because everything else depends on it.
 *
 * TEAMS ARE THE DEFAULT UNIT
 * --------------------------
 * Barrelo is a team platform first: a match is N teams, and a solo match is simply N teams of one. So
 * the fold below scores, rotates and ranks *teams*, never players — see `buildTeams()`. Writing it the
 * other way round (per-player, teams bolted on later) means reworking every rule you write, so the
 * template starts you on the shape that scales both ways.
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

/**
 * One side in the match. A solo player is a team of one — nothing else in this file special-cases it,
 * which is exactly why solo keeps working for free as you write team rules.
 */
export interface Team {
  /** The group index from `payload.playerGroups`, or the player's own roster position when ungrouped. */
  groupIndex: number;
  /** Members in roster order. The order the team takes its turns in. */
  playerIds: string[];
}

export interface GameState {
  /** The match's teams, ordered by group index. Solo play yields one single-member team per player. */
  teams: Team[];
  /** Whose turn it is. Barrelo can't know this — turn order is a rule — so the board reports it back up. */
  currentPlayerId: string | null;
  /** Which team that player is throwing for, for the board's turn highlight. */
  currentGroupIndex: number | null;
  /** Darts thrown in the visit currently in progress, for the shell's 1/2/3 slots. */
  currentVisitThrows: DetectedThrow[];
  /** TODO: replace with whatever your game tracks *per team* — score, marks, lives, positions... */
  scoreByGroup: Record<number, number>;
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

/**
 * Resolves a player's team: their explicit assignment in `playerGroups` if present, otherwise their own
 * roster position — an implicit team of one. Mirrors the host's `GameSetupExtensions.EffectiveGroupIndex`
 * exactly, which is what makes a game written against teams still play correctly when Barrelo hands it an
 * ungrouped roster.
 */
export function effectiveGroupIndex(payload: ClientGamePayload, playerId: string): number {
  const assigned = payload?.playerGroups?.[playerId];
  if (typeof assigned === "number") return assigned;
  return (payload?.playerIds ?? []).indexOf(playerId);
}

/**
 * Folds the roster into teams, ordered by group index and each holding its members in roster order.
 * Only teams with at least one member exist — Barrelo's start screen hands over contiguous group
 * indices, but an empty bucket would otherwise become a phantom side that can never throw.
 */
export function buildTeams(payload: ClientGamePayload): Team[] {
  const playerIds = payload?.playerIds ?? [];
  const byGroup = new Map<number, string[]>();

  playerIds.forEach((playerId) => {
    const groupIndex = effectiveGroupIndex(payload, playerId);
    const members = byGroup.get(groupIndex);
    if (members) members.push(playerId);
    else byGroup.set(groupIndex, [playerId]);
  });

  return [...byGroup.entries()]
    .sort(([a], [b]) => a - b)
    .map(([groupIndex, memberIds]) => ({ groupIndex, playerIds: memberIds }));
}

/** A visit is over when the turn boundary arrived, or when three darts have been thrown. */
function isVisitOver(visit: Visit): boolean {
  return visit.ended || visit.throws.length >= 3;
}

/**
 * TODO: your rules. This template just counts each team's score and rotates turns — one visit per team
 * per round, with each team rotating its own thrower — never finishing. Enough to prove the pipeline
 * works, not enough to be a game.
 *
 * Note the rotation is over *teams*, not over the flat roster: a three-player team would otherwise get
 * three visits a round against a two-player team's two, which decides most games on roster size alone.
 * If your game genuinely wants the flat walk (Cricket does), that's a rules decision to make here —
 * deliberately, not by accident.
 */
export function replay(payload: ClientGamePayload): GameState {
  // Defaulted rather than destructured straight out: a board can be rendered before the first real
  // snapshot arrives (and the dev harness starts with an empty match), and a crash here is a blank
  // screen with a stack trace in a sandboxed iframe nobody is watching.
  const visits = payload?.visits ?? [];
  const teams = buildTeams(payload);

  const scoreByGroup: Record<number, number> = {};
  teams.forEach((team) => {
    scoreByGroup[team.groupIndex] = 0;
  });

  if (teams.length === 0) {
    return {
      teams,
      currentPlayerId: null,
      currentGroupIndex: null,
      currentVisitThrows: [],
      scoreByGroup,
      winnerPlayerIds: [],
      finalStandings: [],
      isComplete: false,
    };
  }

  let teamIndex = 0;
  // Which member each team throws next — a team's own rotation, so it survives the other teams' visits.
  const memberIndexByGroup: Record<number, number> = {};
  teams.forEach((team) => {
    memberIndexByGroup[team.groupIndex] = 0;
  });
  let currentVisitThrows: DetectedThrow[] = [];

  visits.forEach((visit) => {
    const team = teams[teamIndex];
    visit.throws.forEach((t) => {
      scoreByGroup[team.groupIndex] += t.score;
    });

    if (isVisitOver(visit)) {
      // TODO: who throws next is a rules decision — an elimination game skips dead teams, and a
      // "hit a bull, throw again" game doesn't advance at all. Barrelo deliberately doesn't assume.
      memberIndexByGroup[team.groupIndex] =
        (memberIndexByGroup[team.groupIndex] + 1) % team.playerIds.length;
      teamIndex = (teamIndex + 1) % teams.length;
      currentVisitThrows = [];
    } else {
      currentVisitThrows = visit.throws;
    }
  });

  // TODO: decide your win condition here, then populate winnerPlayerIds and finalStandings. Until
  // isComplete goes true the match never ends and no leaderboard points are awarded. Both lists are
  // player ids — a winning *team* contributes all of its members, and standings list each team's
  // members together, best team first.
  const winnerPlayerIds: string[] = [];

  const currentTeam = teams[teamIndex];
  return {
    teams,
    currentPlayerId: currentTeam.playerIds[memberIndexByGroup[currentTeam.groupIndex]],
    currentGroupIndex: currentTeam.groupIndex,
    currentVisitThrows,
    scoreByGroup,
    winnerPlayerIds,
    finalStandings: winnerPlayerIds.length > 0 ? teams.flatMap((team) => team.playerIds) : [],
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
    JSON.stringify([state.currentPlayerId, state.scoreByGroup, state.winnerPlayerIds, state.isComplete])
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
