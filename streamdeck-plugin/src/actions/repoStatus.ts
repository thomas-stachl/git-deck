import {
  action,
  type DidReceiveSettingsEvent,
  type KeyAction,
  type KeyDownEvent,
  SingletonAction,
  type WillAppearEvent,
  type WillDisappearEvent,
} from "@elgato/streamdeck";
import { gitDeckIpc } from "../ipc/sharedClient";
import type { RepositoryOverview } from "../ipc/gitDeckIpc.types";
import type { GitDeckKeySettings } from "./settings";

/** Minutes, not constant polling — same "quiet background fetch" philosophy the design doc names. */
const REFRESH_INTERVAL_MS = 2 * 60 * 1000;

type BadgeState = "upToDate" | "behind" | "noUpstream" | "error" | "disconnected" | "none";

const BADGE_COLOR: Record<BadgeState, string> = {
  upToDate: "#16a34a",
  behind: "#2563eb",
  noUpstream: "#6b7280",
  error: "#d97706",
  disconnected: "#dc2626",
  none: "#374151",
};

/**
 * Designed artwork for the two states that have it (from Downloads/Gitdeck App Icon Design/
 * streamdeck/status-uptodate.svg and status-behind.svg, copied verbatim) — the other four states
 * fall back to a plain generated background until matching artwork exists for them too.
 */
const DESIGNED_BADGE_SVG: Partial<Record<BadgeState, string>> = {
  upToDate: `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
  <rect width="144" height="144" fill="#151515"></rect>
  <circle cx="72" cy="72" r="46" fill="none" stroke="#44CB62" stroke-width="10"></circle>
  <path d="M50 73 L66 89 L96 55" fill="none" stroke="#44CB62" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"></path>
</svg>`,
  behind: `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
  <rect width="144" height="144" fill="#151515"></rect>
  <path d="M72 26 V92" fill="none" stroke="#204CFE" stroke-width="12" stroke-linecap="round"></path>
  <path d="M44 68 L72 96 L100 68" fill="none" stroke="#204CFE" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"></path>
  <path d="M38 118 H106" fill="none" stroke="#FFFFFF" stroke-width="10" stroke-linecap="round"></path>
</svg>`,
};

/**
 * Press pulls if behind, else opens the Branches palette scoped to this key's repo; the face shows
 * live ahead/behind. Reuses IBranchService's own ahead/behind semantics via GetStatusAsync — no
 * duplicated git logic here, same principle the .NET side follows.
 */
@action({ UUID: "com.gitdeck.plugin.repo-status" })
export class RepoStatus extends SingletonAction<GitDeckKeySettings> {
  private readonly refreshTimers = new Map<string, NodeJS.Timeout>();

  override onWillAppear(ev: WillAppearEvent<GitDeckKeySettings>): void {
    // The manifest only declares Controllers: ["Keypad"] for these actions, so ev.action is always
    // a KeyAction at runtime — but SingletonAction's events type it as DialAction | KeyAction
    // since the base class covers both controller kinds. Narrow explicitly rather than casting.
    if (!ev.action.isKey()) {
      return;
    }

    const repositoryPath = ev.payload.settings.repositoryPath;
    const action = ev.action;

    void this.refreshFace(action, repositoryPath);

    this.refreshTimers.set(
      action.id,
      setInterval(() => void this.refreshFace(action, repositoryPath), REFRESH_INTERVAL_MS),
    );
  }

  override onWillDisappear(ev: WillDisappearEvent<GitDeckKeySettings>): void {
    const timer = this.refreshTimers.get(ev.action.id);

    if (timer) {
      clearInterval(timer);
      this.refreshTimers.delete(ev.action.id);
    }
  }

  override async onKeyDown(ev: KeyDownEvent<GitDeckKeySettings>): Promise<void> {
    const repositoryPath = ev.payload.settings.repositoryPath;

    if (!repositoryPath) {
      await ev.action.showAlert();
      return;
    }

    try {
      const overview = await gitDeckIpc.getStatus(repositoryPath);

      if (overview.HasUpstream && overview.BehindBy > 0) {
        await gitDeckIpc.pull(repositoryPath);
      } else {
        await gitDeckIpc.openBranches(repositoryPath);
      }
    } catch {
      await ev.action.showAlert();
    }

    await this.refreshFace(ev.action, repositoryPath);
  }

  override async onDidReceiveSettings(ev: DidReceiveSettingsEvent<GitDeckKeySettings>): Promise<void> {
    if (!ev.action.isKey()) {
      return;
    }

    await this.refreshFace(ev.action, ev.payload.settings.repositoryPath);
  }

  private async refreshFace(action: KeyAction<GitDeckKeySettings>, repositoryPath?: string): Promise<void> {
    if (!repositoryPath) {
      await action.setTitle("No repo");
      await action.setImage(backgroundSvg("none"));
      return;
    }

    if (!gitDeckIpc.isConnected) {
      // Distinct "disconnected" face + auto-retry: GitDeckIpcClient reconnects itself on a backoff,
      // and onDidReceiveSettings / the next refresh tick will pick the real status back up once it
      // does — nothing else to trigger here.
      await action.setTitle("GitDeck.App\nnot running");
      await action.setImage(backgroundSvg("disconnected"));
      return;
    }

    try {
      const overview = await gitDeckIpc.getStatus(repositoryPath);

      if (!overview.IsRepository) {
        await action.setTitle(overview.LoadError ? "Load error" : "Not a repo");
        await action.setImage(backgroundSvg(overview.LoadError ? "error" : "none"));
        return;
      }

      await action.setTitle(describeStatus(overview));
      await action.setImage(backgroundSvg(badgeStateFor(overview)));
    } catch {
      await action.setTitle("Error");
      await action.setImage(backgroundSvg("error"));
    }
  }
}

function badgeStateFor(overview: RepositoryOverview): BadgeState {
  if (!overview.HasUpstream) {
    return "noUpstream";
  }

  return overview.BehindBy > 0 ? "behind" : "upToDate";
}

/**
 * Stream Deck shrinks/wraps overlong title text on its own, but does it badly enough to be worth
 * pre-empting. 12 characters ("streamdeck-…") already turned out to still be too wide — confirmed
 * from the plugin's own log, that exact string was handed to setTitle and Stream Deck *still*
 * rendered it as a mangled mid-word fragment. Cut further and re-check rather than trusting this
 * number; there's no way to verify actual key rendering from outside a real device.
 */
const MAX_BRANCH_NAME_LENGTH = 8;

function shortenBranchName(name: string): string {
  return name.length > MAX_BRANCH_NAME_LENGTH
    ? `${name.slice(0, MAX_BRANCH_NAME_LENGTH - 1)}…`
    : name;
}

/**
 * "main / -3 +1" for diverged history, or just the branch name once neither (or when there's no
 * upstream to compare against) — the up-to-date/behind badge already carries that distinction
 * visually, so the title doesn't need to spell out "up to date" too. Plain +/- rather than ↓/↑:
 * one less Unicode glyph that might not render cleanly at key-title size, and it matches the
 * +N/-N convention this repo's own shell prompt already uses for ahead/behind.
 */
function describeStatus(overview: RepositoryOverview): string {
  const branch = shortenBranchName(overview.Head ?? "?");

  if (!overview.HasUpstream || (overview.BehindBy === 0 && overview.AheadBy === 0)) {
    return branch;
  }

  const parts: string[] = [];
  if (overview.BehindBy > 0) parts.push(`-${overview.BehindBy}`);
  if (overview.AheadBy > 0) parts.push(`+${overview.AheadBy}`);

  return `${branch}\n${parts.join(" ")}`;
}

/**
 * The key's full background; setTitle renders the branch name as text on top of it. Uses the
 * designed artwork where it exists (upToDate, behind), otherwise falls back to a plain
 * color-coded generated rect. setImage accepts a raw SVG string directly either way.
 */
function backgroundSvg(state: BadgeState): string {
  const designed = DESIGNED_BADGE_SVG[state];
  if (designed) {
    return designed;
  }

  const color = BADGE_COLOR[state];
  return `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144"><rect width="144" height="144" rx="18" fill="${color}"/></svg>`;
}
