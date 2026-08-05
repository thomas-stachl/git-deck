import { action, type KeyDownEvent, SingletonAction } from "@elgato/streamdeck";
import { gitDeckIpc } from "../ipc/sharedClient";
import type { GitDeckKeySettings } from "./settings";

/**
 * Pushes the current branch to its remote — publishes it first (git push --set-upstream) if it
 * has no upstream configured yet, the same auto-publish idea CreateBranchAsync already offers, so
 * this key works whether or not the branch has ever been pushed before.
 */
@action({ UUID: "com.gitdeck.plugin.push" })
export class PushAction extends SingletonAction<GitDeckKeySettings> {
  override async onKeyDown(ev: KeyDownEvent<GitDeckKeySettings>): Promise<void> {
    const repositoryPath = ev.payload.settings.repositoryPath;

    if (!repositoryPath) {
      await ev.action.showAlert();
      return;
    }

    try {
      const result = await gitDeckIpc.push(repositoryPath);
      await (result.IsPushed ? ev.action.showOk() : ev.action.showAlert());
    } catch {
      await ev.action.showAlert();
    }
  }
}
