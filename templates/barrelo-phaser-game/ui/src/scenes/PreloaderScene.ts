import Phaser from "phaser";

/**
 * Loads every asset the real game needs, with a visible progress bar — standard Phaser project
 * structure so a real game's (larger) asset list has somewhere to go without touching BootScene or
 * BoardScene. Replace the two placeholder loads below with your own sprites/spritesheets/atlases.
 */
export class PreloaderScene extends Phaser.Scene {
  constructor() {
    super("preloader");
  }

  preload(): void {
    const { width, height } = this.scale;

    const barWidth = Math.min(320, width * 0.6);
    const barHeight = 18;
    const barX = width / 2 - barWidth / 2;
    const barY = height / 2 - barHeight / 2;

    const track = this.add.rectangle(barX, barY, barWidth, barHeight, 0x24352c).setOrigin(0, 0);
    const fill = this.add.rectangle(barX + 2, barY + 2, 1, barHeight - 4, 0xd9b23d).setOrigin(0, 0);

    this.load.on(Phaser.Loader.Events.PROGRESS, (progress: number) => {
      fill.width = Math.max(1, (barWidth - 4) * progress);
    });
    this.load.on(Phaser.Loader.Events.COMPLETE, () => {
      track.destroy();
      fill.destroy();
    });

    // TODO: replace with your own board assets — spritesheets, atlases, audio, etc.
    this.load.image("token", "assets/token.png");
    this.load.image("board-bg", "assets/board-bg.png");
  }

  create(): void {
    this.scene.start("board");
  }
}
