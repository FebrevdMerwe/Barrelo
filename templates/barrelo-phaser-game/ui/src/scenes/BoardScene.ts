import Phaser from "phaser";
import { GAME_STATE_EVENT, gameStateEvents, getLatestUpdate, type BoardUpdate } from "../bridge";

interface PlayerToken {
  container: Phaser.GameObjects.Container;
  sprite: Phaser.GameObjects.Image;
  ring: Phaser.GameObjects.Arc;
  label: Phaser.GameObjects.Text;
  lastScore: number;
}

const PLAYER_COLORS = [0xd98a3d, 0x4fa3c4, 0xc0546e, 0x6fae6a, 0xa88bc4];

/**
 * TODO: this is the scene to replace with your actual game board. It's kept deliberately simple — one
 * token per player laid out in a row, a ring around whoever's turn it is, and a scale-pop tween whenever
 * a player's score changes — to prove the Scene lifecycle + replay bridge + a Tween all work end to end,
 * without pretending to be a real game.
 *
 * Note it renders the state derived by rules.ts, never the raw payload: Barrelo's snapshot deliberately
 * doesn't say whose turn it is, because that's a rule this game owns.
 */
export class BoardScene extends Phaser.Scene {
  private tokensByPlayerId = new Map<string, PlayerToken>();

  constructor() {
    super("board");
  }

  create(): void {
    const { width, height } = this.scale;
    this.add.image(width / 2, height / 2, "board-bg").setDisplaySize(width, height);

    this.scale.on(Phaser.Scale.Events.RESIZE, this.handleResize, this);

    const onGameState = (update: BoardUpdate) => {
      this.render(update);
    };
    gameStateEvents.on(GAME_STATE_EVENT, onGameState);

    // Barrelo pushes only when state changes, and the push on page load usually lands while Boot and
    // Preloader are still running — so catch up on whatever arrived before this scene existed.
    const missed = getLatestUpdate();
    if (missed) this.render(missed);

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      gameStateEvents.off(GAME_STATE_EVENT, onGameState);
      this.scale.off(Phaser.Scale.Events.RESIZE, this.handleResize, this);
    });
  }

  private handleResize(): void {
    // TODO: re-layout on resize once your board's positions depend on scale.width/scale.height.
  }

  private render({ state, payload, playerNames }: BoardUpdate): void {
    const playerIds = payload.playerIds;
    const { width, height } = this.scale;

    playerIds.forEach((playerId, index) => {
      const x = width * ((index + 1) / (playerIds.length + 1));
      const y = height / 2;
      const score = state.scoreByPlayer[playerId] ?? 0;

      let token = this.tokensByPlayerId.get(playerId);
      if (!token) {
        const sprite = this.add.image(0, 0, "token").setTint(PLAYER_COLORS[index % PLAYER_COLORS.length]);
        const ring = this.add.circle(0, 0, 28).setStrokeStyle(3, 0xd9b23d, 0).setFillStyle(0, 0);
        const label = this.add
          .text(0, 36, `${playerNames[playerId] ?? "Player"} — ${score}`, {
            fontFamily: "monospace",
            fontSize: "14px",
            color: "#e9e4d6",
          })
          .setOrigin(0.5, 0);

        const container = this.add.container(x, y, [ring, sprite, label]);
        token = { container, sprite, ring, label, lastScore: score };
        this.tokensByPlayerId.set(playerId, token);
      } else {
        token.container.setPosition(x, y);
        token.label.setText(`${playerNames[playerId] ?? "Player"} — ${score}`);
      }

      const isCurrentPlayer = playerId === state.currentPlayerId;
      token.ring.setStrokeStyle(3, 0xd9b23d, isCurrentPlayer ? 1 : 0);

      if (score !== token.lastScore) {
        token.lastScore = score;
        this.tweens.add({
          targets: token.sprite,
          scale: { from: 1.4, to: 1 },
          duration: 220,
          ease: "Back.Out",
        });
      }
    });

    // Remove tokens for players no longer present (defensive — the template never removes players).
    for (const [playerId, token] of this.tokensByPlayerId) {
      if (!playerIds.includes(playerId)) {
        token.container.destroy();
        this.tokensByPlayerId.delete(playerId);
      }
    }
  }
}
