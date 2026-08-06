import { action, type KeyDownEvent, SingletonAction } from "@elgato/streamdeck";
import { gitDeckIpc } from "../ipc/sharedClient";
import type { GitDeckKeySettings } from "./settings";

/** Silent refresh only, for anyone who wants fetch decoupled from pull. */
@action({ UUID: "com.gitdeck.plugin.fetch" })
export class FetchAction extends SingletonAction<GitDeckKeySettings> {
  override async onKeyDown(ev: KeyDownEvent<GitDeckKeySettings>): Promise<void> {
    const repositoryPath = ev.payload.settings.repositoryPath;

    if (!repositoryPath) {
      await ev.action.showAlert();
      return;
    }

    try {
      const result = await gitDeckIpc.fetch(repositoryPath);
      await (result.IsDone ? ev.action.showOk() : ev.action.showAlert());
    } catch {
      await ev.action.showAlert();
    }
  }
}
