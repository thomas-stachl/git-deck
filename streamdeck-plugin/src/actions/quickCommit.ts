import { action, type KeyDownEvent, SingletonAction } from "@elgato/streamdeck";
import { gitDeckIpc } from "../ipc/sharedClient";
import type { GitDeckKeySettings } from "./settings";

/**
 * Opens the Commit palette scoped to this key's repo — reuses the existing AI commit-message
 * generation rather than trying to commit blind from a keypad with no display beyond a title/icon.
 */
@action({ UUID: "com.gitdeck.plugin.quick-commit" })
export class QuickCommit extends SingletonAction<GitDeckKeySettings> {
  override async onKeyDown(ev: KeyDownEvent<GitDeckKeySettings>): Promise<void> {
    const repositoryPath = ev.payload.settings.repositoryPath;

    if (!repositoryPath) {
      await ev.action.showAlert();
      return;
    }

    try {
      await gitDeckIpc.openCommit(repositoryPath);
    } catch {
      await ev.action.showAlert();
    }
  }
}
