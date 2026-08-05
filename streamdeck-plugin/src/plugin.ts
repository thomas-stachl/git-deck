import streamDeck from "@elgato/streamdeck";
import { FetchAction } from "./actions/fetchAction";
import { QuickCommit } from "./actions/quickCommit";
import { RepoStatus } from "./actions/repoStatus";
import { gitDeckIpc } from "./ipc/sharedClient";

streamDeck.logger.setLevel("trace");

/**
 * The shared Property Inspector's two custom messages — populating its recent-repo dropdown and
 * its "Browse…" button — both need an answer from GitDeck.App. Centralized here via streamDeck.ui's
 * plugin-wide listener rather than duplicated across all three actions' own onSendToPlugin, since
 * every action shares the same PI and none of this is action-specific.
 */
streamDeck.ui.onSendToPlugin(async (ev) => {
  const payload = ev.payload;

  if (!isRecord(payload)) {
    return;
  }

  // sdpi-select's datasource mechanism (ui/repository-picker.html's <sdpi-select datasource=
  // "getRecentRepositories" hot-reload>) requests this event name itself and expects the response's
  // `event` field to echo it back, carrying an `items: {label, value}[]` array — not an
  // invented response name.
  if (payload.event === "getRecentRepositories") {
    await sendRecentRepositories();
    return;
  }

  if (payload.event === "pickFolder") {
    const path = await gitDeckIpc.pickRepositoryFolder().catch(() => null);
    await streamDeck.ui.sendToPropertyInspector({ event: "pickedFolder", path });

    // The pick just updated GitDeck.App's MRU (GitDeckIpc.PickRepositoryFolderAsync records it) —
    // push a fresh list so the select's options include the newly-picked path, not just whatever
    // was current when the PI first loaded.
    if (path) {
      await sendRecentRepositories();
    }
  }
});

async function sendRecentRepositories(): Promise<void> {
  const paths = await gitDeckIpc.getRecentRepositories().catch(() => [] as string[]);
  await streamDeck.ui.sendToPropertyInspector({
    event: "getRecentRepositories",
    items: paths.map((path) => ({ label: path, value: path })),
  });
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

streamDeck.actions.registerAction(new RepoStatus());
streamDeck.actions.registerAction(new FetchAction());
streamDeck.actions.registerAction(new QuickCommit());

// Connects to GitDeck.App over the named pipe; reconnects itself forever after this, independent
// of the Stream Deck WebSocket connection below.
gitDeckIpc.start();

streamDeck.connect();
