import Phaser from "phaser";
import { GAME_STATE_EVENT, gameStateEvents, getLatestUpdate, type BoardUpdate } from "../bridge";

interface TeamToken {
  container: Phaser.GameObjects.Container;
  sprite: Phaser.GameObjects.Image;
  ring: Phaser.GameObjects.Arc;
  label: Phaser.GameObjects.Text;
  lastScore: number;
}

const TEAM_COLORS = [0xd98a3d, 0x4fa3c4, 0xc0546e, 0x6fae6a, 0xa88bc4];

/**
 * TODO: this is the scene to replace with your actual game board. It's kept deliberately simple — one
 * token per *team* laid out in a row, a ring around whichever team is throwing, and a scale-pop tween
 * whenever a team's score changes — to prove the Scene lifecycle + replay bridge + a Tween all work end
 * to end, without pretending to be a real game.
 *
 * A token per team rather than per player is the point: that's Barrelo's default shape, and a solo match
 * renders identically because solo is just teams of one. The thrower's name is marked within the team's
 * label, so a four-player team is still one token, not four.
 *
 * Note it renders the state derived by rules.ts, never the raw payload: Barrelo's snapshot deliberately
 * doesn't say whose turn it is, because that's a rule this game owns.
 */
export class BoardScene extends Phaser.Scene {
  private tokensByGroup = new Map<number, TeamToken>();

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

  /**
   * "Alex" for a solo team; "> Alex / Sam" for a team, with the thrower marked. A one-member team reads
   * exactly like a solo player, which is what keeps the two modes looking like one game.
   */
  private teamLabel(
    playerIds: string[],
    playerNames: Record<string, string>,
    currentPlayerId: string | null
  ): string {
    return playerIds
      .map((id) => {
        const name = playerNames[id] ?? "Player";
        return playerIds.length > 1 && id === currentPlayerId ? `> ${name}` : name;
      })
      .join(" / ");
  }

  private render({ state, playerNames }: BoardUpdate): void {
    const { teams } = state;
    const { width, height } = this.scale;

    teams.forEach((team, index) => {
      const x = width * ((index + 1) / (teams.length + 1));
      const y = height / 2;
      const score = state.scoreByGroup[team.groupIndex] ?? 0;
      const text = `${this.teamLabel(team.playerIds, playerNames, state.currentPlayerId)} — ${score}`;

      let token = this.tokensByGroup.get(team.groupIndex);
      if (!token) {
        const sprite = this.add.image(0, 0, "token").setTint(TEAM_COLORS[index % TEAM_COLORS.length]);
        const ring = this.add.circle(0, 0, 28).setStrokeStyle(3, 0xd9b23d, 0).setFillStyle(0, 0);
        const label = this.add
          .text(0, 36, text, {
            fontFamily: "monospace",
            fontSize: "14px",
            color: "#e9e4d6",
            align: "center",
          })
          .setOrigin(0.5, 0);

        const container = this.add.container(x, y, [ring, sprite, label]);
        token = { container, sprite, ring, label, lastScore: score };
        this.tokensByGroup.set(team.groupIndex, token);
      } else {
        token.container.setPosition(x, y);
        token.label.setText(text);
      }

      const isThrowing = team.groupIndex === state.currentGroupIndex;
      token.ring.setStrokeStyle(3, 0xd9b23d, isThrowing ? 1 : 0);

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

    // Remove tokens for teams no longer present (defensive — the template never removes teams).
    const liveGroups = new Set(teams.map((team) => team.groupIndex));
    for (const [groupIndex, token] of this.tokensByGroup) {
      if (!liveGroups.has(groupIndex)) {
        token.container.destroy();
        this.tokensByGroup.delete(groupIndex);
      }
    }
  }
}
