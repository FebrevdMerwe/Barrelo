import Phaser from "phaser";

/**
 * First scene in the boot sequence. Phaser convention: Boot loads only what the *next* scene
 * (Preloader) needs to render itself — usually nothing more than a loading-bar frame — then hands off
 * immediately. This template needs nothing that early, so it just starts Preloader.
 */
export class BootScene extends Phaser.Scene {
  constructor() {
    super("boot");
  }

  create(): void {
    this.scene.start("preloader");
  }
}
