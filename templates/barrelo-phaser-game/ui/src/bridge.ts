import Phaser from "phaser";
import type {
  BarreloDisplayMessage,
  BarreloGameStateMessage,
  BarreloMatchCompleteMessage,
  ClientGamePayload,
} from "../../shared/types";
import { hashLog, hashState, replay, type GameState } from "./rules";

/**
 * The whole boundary between Barrelo and this game.
 *
 * Down: Barrelo's page embeds ui/index.html in a sandboxed <iframe> and posts
 * `{ type: "barrelo:gameState", snapshot, playerNames }` on every state change. The snapshot carries the
 * visit log; the rules are re-derived from it here, in `replay()`.
 *
 * Up: this game owns its own rules, so Barrelo doesn't know whose turn it is or when the match is over.
 * Both are reported back — display hints on every state change, and the final result exactly once.
 *
 * Scenes subscribe to GAME_STATE_EVENT rather than reading window.postMessage directly, so they never
 * need to know how any of this arrives.
 */
export const GAME_STATE_EVENT = "barrelo:gameState";

export interface BoardUpdate {
  state: GameState;
  payload: ClientGamePayload;
  playerNames: Record<string, string>;
}

export const gameStateEvents = new Phaser.Events.EventEmitter();

/**
 * The last update received, retained because Barrelo pushes only when state *changes*. A board that is
 * still booting — Phaser's Boot/Preloader scenes run before BoardScene subscribes — would otherwise miss
 * the push that arrives on page load and sit blank until the next dart. Scenes call this in create() to
 * catch up, then rely on the event from then on.
 */
let latestUpdate: BoardUpdate | null = null;

export function getLatestUpdate(): BoardUpdate | null {
  return latestUpdate;
}

function postToBarrelo(message: BarreloDisplayMessage | BarreloMatchCompleteMessage): void {
  // Only meaningful when embedded; standalone `npm run dev` has no host to talk to.
  if (window.parent === window.self) return;
  window.parent.postMessage(message, window.location.origin);
}

let reportedComplete = false;

function publish(snapshot: BarreloGameStateMessage["snapshot"], playerNames: Record<string, string>): void {
  const payload = snapshot.payload;
  const state = replay(payload);

  latestUpdate = { state, payload, playerNames };
  gameStateEvents.emit(GAME_STATE_EVENT, latestUpdate);

  postToBarrelo({
    type: "barrelo:display",
    currentPlayerId: state.currentPlayerId,
    visitThrows: state.currentVisitThrows,
    logHash: hashLog(payload),
    stateHash: hashState(state),
  });

  // Reported once and only once: Barrelo ends the session on the first one, so a second would arrive
  // when there is no longer an active match to end.
  if (state.isComplete && !reportedComplete) {
    reportedComplete = true;
    postToBarrelo({
      type: "barrelo:matchComplete",
      winnerPlayerIds: state.winnerPlayerIds,
      finalStandings: state.finalStandings,
    });
  }
}

window.addEventListener("message", (event: MessageEvent) => {
  const data = event.data as Partial<BarreloGameStateMessage> | undefined;
  if (!data || data.type !== "barrelo:gameState" || !data.snapshot) return;
  publish(data.snapshot, data.playerNames ?? {});
});

// Dev-mode preview: when running standalone via `npm run dev` (no Barrelo host iframe around us), there's
// nothing posting real snapshots in. Feed a canned payload so the board is visible while you iterate on
// rendering. Never runs in a production build or when actually embedded.
if (import.meta.env.DEV && window.self === window.top) {
  const samplePlayerIds = ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"];
  const samplePayload: ClientGamePayload = {
    seed: 12345,
    playerIds: samplePlayerIds,
    options: {},
    playerGroups: { [samplePlayerIds[0]]: 0, [samplePlayerIds[1]]: 1 },
    visits: [],
  };

  window.setTimeout(() => {
    latestUpdate = {
      state: replay(samplePayload),
      payload: samplePayload,
      playerNames: {
        [samplePlayerIds[0]]: "Alex",
        [samplePlayerIds[1]]: "Sam",
      },
    };
    gameStateEvents.emit(GAME_STATE_EVENT, latestUpdate);
  }, 200);
}
