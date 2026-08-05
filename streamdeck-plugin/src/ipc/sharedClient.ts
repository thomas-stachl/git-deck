import { GitDeckIpcClient } from "./gitDeckIpcClient";

/** One connection, shared by all three actions and the Property Inspector handler in plugin.ts. */
export const gitDeckIpc = new GitDeckIpcClient();
