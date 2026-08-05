import * as net from "node:net";
import {
  createMessageConnection,
  SocketMessageReader,
  SocketMessageWriter,
  type MessageConnection,
} from "vscode-jsonrpc/node";
import type { FetchResult, PullResult, RepositoryOverview } from "./gitDeckIpc.types";

// Must match GitDeck.Ipc/GitDeckIpcConstants.cs's PipeName exactly — no shared codegen across the
// language boundary, so if that constant ever changes, this has to change by hand too.
const PIPE_PATH = "\\\\.\\pipe\\GitDeck.Ipc";

const INITIAL_RECONNECT_DELAY_MS = 1000;
const MAX_RECONNECT_DELAY_MS = 5000;

export type GitDeckConnectionState = "connected" | "disconnected";

/**
 * Talks to GitDeck.App's IGitDeckIpc over the named pipe. Verified against a real
 * GitDeckIpcServer (see the round-trip check run alongside this file): SocketMessageReader/Writer
 * wrap the net.Socket, createMessageConnection speaks the same Content-Length-header JSON-RPC 2.0
 * framing StreamJsonRpc's HeaderDelimitedMessageHandler uses, and calling sendRequest with a single
 * plain value (not wrapped in an array) produces a one-element positional params array on the wire
 * via vscode-jsonrpc's ParameterStructures.auto — exactly the shape StreamJsonRpc expects.
 *
 * GitDeck.App is not always running: reconnects itself on a capped exponential backoff whenever
 * the pipe isn't there, and notifies subscribers via onConnectionChange so open key instances can
 * flip to the disconnected face immediately rather than waiting on their own next call to fail.
 */
export class GitDeckIpcClient {
  private connection: MessageConnection | undefined;
  private reconnectDelayMs = INITIAL_RECONNECT_DELAY_MS;
  private reconnectTimer: NodeJS.Timeout | undefined;
  private disposed = false;
  private readonly connectionChangeListeners = new Set<(state: GitDeckConnectionState) => void>();

  get isConnected(): boolean {
    return this.connection !== undefined;
  }

  /** Returns an unsubscribe function. */
  onConnectionChange(listener: (state: GitDeckConnectionState) => void): () => void {
    this.connectionChangeListeners.add(listener);
    return () => this.connectionChangeListeners.delete(listener);
  }

  /** Starts connecting; call once at plugin startup. Reconnects itself forever after this. */
  start(): void {
    this.connectNow();
  }

  dispose(): void {
    this.disposed = true;
    clearTimeout(this.reconnectTimer);
    this.connection?.dispose();
    this.connection = undefined;
  }

  private connectNow(): void {
    if (this.disposed) {
      return;
    }

    const socket = net.connect({ path: PIPE_PATH });
    let connected = false;

    socket.once("connect", () => {
      connected = true;

      const reader = new SocketMessageReader(socket);
      const writer = new SocketMessageWriter(socket);
      const connection = createMessageConnection(reader, writer);

      connection.onClose(() => this.handleDisconnect());
      connection.onError(() => this.handleDisconnect());
      connection.listen();

      this.connection = connection;
      this.reconnectDelayMs = INITIAL_RECONNECT_DELAY_MS;
      this.notify("connected");
    });

    // "GitDeck.App isn't running yet" is an ordinary, expected state here — not logged as an
    // error, just retried.
    socket.once("error", () => {
      if (!connected) {
        this.scheduleReconnect();
      }
    });

    socket.once("close", () => {
      if (connected) {
        this.handleDisconnect();
      }
    });
  }

  private handleDisconnect(): void {
    if (this.connection === undefined) {
      return;
    }

    this.connection.dispose();
    this.connection = undefined;
    this.notify("disconnected");
    this.scheduleReconnect();
  }

  private scheduleReconnect(): void {
    if (this.disposed) {
      return;
    }

    clearTimeout(this.reconnectTimer);
    this.reconnectTimer = setTimeout(() => this.connectNow(), this.reconnectDelayMs);
    this.reconnectDelayMs = Math.min(this.reconnectDelayMs * 2, MAX_RECONNECT_DELAY_MS);
  }

  private notify(state: GitDeckConnectionState): void {
    for (const listener of this.connectionChangeListeners) {
      listener(state);
    }
  }

  private requireConnection(): MessageConnection {
    if (!this.connection) {
      throw new Error("GitDeck.App is not reachable.");
    }

    return this.connection;
  }

  // Method names and parameter order mirror GitDeck.Ipc/IGitDeckIpc.cs exactly — the literal C#
  // member name, Async suffix included; StreamJsonRpc does no name translation. Every C# method
  // also takes a trailing CancellationToken, simply omitted here — the spike confirmed the
  // omitted-optional-trailing-parameter binds to its default just fine.

  getStatus(repositoryPath: string): Promise<RepositoryOverview> {
    return this.requireConnection().sendRequest<RepositoryOverview>("GetStatusAsync", repositoryPath);
  }

  fetch(repositoryPath: string): Promise<FetchResult> {
    return this.requireConnection().sendRequest<FetchResult>("FetchAsync", repositoryPath);
  }

  pull(repositoryPath: string): Promise<PullResult> {
    return this.requireConnection().sendRequest<PullResult>("PullAsync", repositoryPath);
  }

  openBranches(repositoryPath: string): Promise<void> {
    return this.requireConnection().sendRequest<void>("OpenBranchesAsync", repositoryPath);
  }

  openCommit(repositoryPath: string): Promise<void> {
    return this.requireConnection().sendRequest<void>("OpenCommitAsync", repositoryPath);
  }

  pickRepositoryFolder(): Promise<string | null> {
    return this.requireConnection().sendRequest<string | null>("PickRepositoryFolderAsync");
  }

  getRecentRepositories(): Promise<string[]> {
    return this.requireConnection().sendRequest<string[]>("GetRecentRepositoriesAsync");
  }
}
