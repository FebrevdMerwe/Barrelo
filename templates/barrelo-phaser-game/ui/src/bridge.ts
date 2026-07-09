import Phaser from "phaser";
import type { BarreloGameStateMessage } from "../../shared/types";

/**
 * The host page (Barrelo's control.js/view.js) embeds ui/index.html in a sandboxed <iframe> and posts
 * `{ type: "barrelo:gameState", snapshot, playerNames }` into it via postMessage on every state push —
 * this is what lets the board be Phaser (or Pixi, or anything else) instead of a global JS function.
 *
 * Scenes subscribe to `GAME_STATE_EVENT` on this emitter rather than reading window.postMessage
 * directly, so BootScene/PreloaderScene/BoardScene never need to know how the message actually arrives.
 */
export const GAME_STATE_EVENT = "barrelo:gameState";

export const gameStateEvents = new Phaser.Events.EventEmitter();

window.addEventListener("message", (event: MessageEvent) => {
  const data = event.data as Partial<BarreloGameStateMessage> | undefined;
  if (!data || data.type !== "barrelo:gameState" || !data.snapshot) return;
  gameStateEvents.emit(GAME_STATE_EVENT, data as BarreloGameStateMessage);
});

// Dev-mode preview: when running standalone via `npm run dev` (no Barrelo host iframe around us),
// there's nothing posting real snapshots in. Feed a canned sample so the board is visible while you
// iterate on rendering, instead of staring at an empty canvas. Never runs in a production build or
// when actually embedded in Barrelo's iframe.
if (import.meta.env.DEV && window.self === window.top) {
  const samplePlayerIds = ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"];
  const sample: BarreloGameStateMessage = {
    type: "barrelo:gameState",
    snapshot: {
      matchId: "00000000-0000-0000-0000-000000000000",
      gameId: "your-game-id",
      status: "InProgress",
      currentPlayerId: samplePlayerIds[0],
      legNumber: 1,
      setNumber: 1,
      recentThrows: [],
      isComplete: false,
      winnerPlayerIds: null,
      payload: {
        throwCountByPlayer: { [samplePlayerIds[0]]: 3, [samplePlayerIds[1]]: 1 },
        currentVisitThrows: [],
      },
    },
    playerNames: {
      [samplePlayerIds[0]]: "Alex",
      [samplePlayerIds[1]]: "Sam",
    },
  };

  window.setTimeout(() => gameStateEvents.emit(GAME_STATE_EVENT, sample), 200);
}
