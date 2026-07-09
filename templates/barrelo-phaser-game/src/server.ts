#!/usr/bin/env node
/**
 * Skeleton implementation of Barrelo's out-of-process game RPC contract — see the top-level README's
 * "Adding a new game" -> "Out-of-process games" section for the full spec, and ../shared/types.ts for
 * the exact wire shapes.
 *
 * Every endpoint below is wired correctly against the real contract (so `dotnet run`-ed Barrelo can
 * spawn this, list it, start a match, and push/pull state without any changes on the host side) but the
 * actual rules are a TODO — see applyThrow/applyEndOfTurn/applyUndo/computeResult. Replace those with
 * your game's logic; leave the rest of the file (HTTP wiring, snapshot envelope) alone.
 *
 * Runs directly via `tsx` (its only runtime dependency) — no separate compile step. For manual/dev use,
 * `npx tsx src/server.ts --port 6100` works from an interactive shell. ../plugin.json's launch.command
 * instead invokes `node node_modules/tsx/dist/cli.mjs ...` directly, because on Windows `npx` resolves
 * to `npx.cmd`, which .NET's Process.Start can't launch without a shell — see this template's README.
 */

import http from "node:http";
import type {
  DetectedThrow,
  GameResult,
  GameSetup,
  GameStateSnapshot,
} from "../shared/types.js";

function argValue(name: string, fallback: string): string {
  const flag = "--" + name;
  const index = process.argv.indexOf(flag);
  return index >= 0 && process.argv[index + 1] !== undefined ? process.argv[index + 1] : fallback;
}

const PORT = Number(argValue("port", "6100"));
const PROTOCOL_VERSION = 1;

type LogEntry = { type: "throw"; throw: DetectedThrow } | { type: "endOfTurn" };

let playerIds: string[] = [];
// Every /throw and /end-turn call, in order — state is always recomputed by replaying this log, which
// makes /undo trivial: just pop the log and recompute. Keep this pattern; only the fold in `replay()`
// needs your game's rules.
let log: LogEntry[] = [];

// TODO: replace this with whatever your game needs to track per player (score, marks, lives...).
interface GameState {
  currentPlayerId: string | null;
  currentVisitThrows: DetectedThrow[];
  throwCountByPlayer: Record<string, number>;
  winnerPlayerIds: string[];
}

// TODO: this is the only place your game's rules actually live. It's a pure fold over `log`, called
// fresh on every /state, /result, and /undo — never mutate state outside of pushing/popping `log`.
function replay(): GameState {
  const throwCountByPlayer: Record<string, number> = {};
  playerIds.forEach((id) => {
    throwCountByPlayer[id] = 0;
  });

  let turnIndex = 0;
  let currentVisitThrows: DetectedThrow[] = [];

  for (const entry of log) {
    if (entry.type === "endOfTurn") {
      turnIndex = (turnIndex + 1) % playerIds.length;
      currentVisitThrows = [];
      continue;
    }

    const playerId = playerIds[turnIndex];
    throwCountByPlayer[playerId] += 1;

    currentVisitThrows.push(entry.throw);
    if (currentVisitThrows.length >= 3) {
      turnIndex = (turnIndex + 1) % playerIds.length;
      currentVisitThrows = [];
    }
  }

  return {
    // TODO: this template never actually finishes a game — winnerPlayerIds always stays empty.
    // Set currentPlayerId to null and populate winnerPlayerIds once your win condition is met.
    currentPlayerId: playerIds.length > 0 ? playerIds[turnIndex] : null,
    currentVisitThrows,
    throwCountByPlayer,
    winnerPlayerIds: [],
  };
}

function snapshot(): GameStateSnapshot {
  const state = replay();
  const recentThrows = log
    .filter((entry): entry is { type: "throw"; throw: DetectedThrow } => entry.type === "throw")
    .slice(-15)
    .map((entry) => entry.throw);

  return {
    // Barrelo overwrites matchId on every call regardless of what's returned here, but the field is
    // non-nullable in the contract, so a real placeholder is required — an empty string fails to parse.
    matchId: "00000000-0000-0000-0000-000000000000",
    gameId: "your-game-id",
    status: state.winnerPlayerIds.length > 0 ? "Complete" : "InProgress",
    currentPlayerId: state.winnerPlayerIds.length > 0 ? null : state.currentPlayerId,
    legNumber: 1,
    setNumber: 1,
    recentThrows,
    isComplete: state.winnerPlayerIds.length > 0,
    winnerPlayerIds: state.winnerPlayerIds.length > 0 ? state.winnerPlayerIds : null,
    // TODO: this is the payload your ui/src/scenes/BoardScene.ts reads — shape it however your board
    // needs (per-player score, targets, board positions...).
    payload: {
      throwCountByPlayer: state.throwCountByPlayer,
      currentVisitThrows: state.currentVisitThrows,
    },
  };
}

function sendJson(res: http.ServerResponse, statusCode: number, body: unknown): void {
  const json = JSON.stringify(body);
  res.writeHead(statusCode, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(json),
  });
  res.end(json);
}

function badRequest(res: http.ServerResponse, message: string): void {
  // Barrelo maps a 400 here straight back onto GameRuleViolationException on the host side.
  sendJson(res, 400, { message });
}

function readJsonBody(req: http.IncomingMessage): Promise<unknown> {
  return new Promise((resolve, reject) => {
    let raw = "";
    req.on("data", (chunk) => {
      raw += chunk;
    });
    req.on("end", () => {
      if (!raw) return resolve(null);
      try {
        resolve(JSON.parse(raw));
      } catch (err) {
        reject(err);
      }
    });
    req.on("error", reject);
  });
}

const server = http.createServer((req, res) => {
  Promise.resolve()
    .then(async () => {
      if (req.method === "GET" && req.url === "/health") {
        return sendJson(res, 200, { status: "ok", protocolVersion: PROTOCOL_VERSION });
      }

      if (req.method === "POST" && req.url === "/create") {
        const body = (await readJsonBody(req)) as Partial<GameSetup> | null;
        if (!body || !Array.isArray(body.playerIds) || body.playerIds.length === 0) {
          return badRequest(res, "This game requires at least one player.");
        }
        playerIds = body.playerIds;
        log = [];
        return sendJson(res, 200, {});
      }

      if (req.method === "POST" && req.url === "/throw") {
        const body = (await readJsonBody(req)) as DetectedThrow | null;
        if (!body) return badRequest(res, "Missing throw body.");
        log.push({ type: "throw", throw: body });
        return sendJson(res, 200, {});
      }

      if (req.method === "POST" && req.url === "/end-turn") {
        log.push({ type: "endOfTurn" });
        return sendJson(res, 200, {});
      }

      if (req.method === "POST" && req.url === "/undo") {
        if (log.length === 0) return badRequest(res, "There is nothing to undo.");
        log.pop();
        return sendJson(res, 200, {});
      }

      if (req.method === "GET" && req.url === "/state") {
        return sendJson(res, 200, snapshot());
      }

      if (req.method === "GET" && req.url === "/result") {
        const state = replay();
        const result: GameResult = {
          winnerPlayerIds: state.winnerPlayerIds,
          // TODO: rank the rest of the field however your game defines placement (e.g. reverse
          // elimination order) — this template just returns original player order.
          finalStandings: playerIds,
        };
        return sendJson(res, 200, result);
      }

      res.writeHead(404);
      res.end();
    })
    .catch((err) => {
      sendJson(res, 500, { message: String((err as Error)?.message ?? err) });
    });
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`Barrelo Phaser game template listening on http://127.0.0.1:${PORT}`);
});
