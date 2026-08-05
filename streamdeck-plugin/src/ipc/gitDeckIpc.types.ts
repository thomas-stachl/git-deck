/**
 * Hand-mirrors GitDeck.Ipc/IGitDeckIpc.cs. There is no shared codegen across the C#/TypeScript
 * boundary — if the C# interface or its DTOs change, these have to be updated by hand.
 *
 * Property casing is PascalCase, matching the C# names exactly. Confirmed empirically: a throwaway
 * interop spike (a real StreamJsonRpc server using SystemTextJsonFormatter, called from a
 * hand-rolled Node client) showed a GetStatusAsync response come back as
 * {"IsRepository":true,"WorkingDirectory":"...","HasUpstream":true,...} — not camelCase.
 */

/** Mirrors GitDeck.Git/Repositories/BranchInfo.cs's BranchInfo record. */
export interface BranchInfo {
  Name: string;
  IsRemote: boolean;
  RemoteName: string | null;
  IsCurrent: boolean;
  ShortName: string;
}

/**
 * Mirrors GitDeck.Git/Repositories/ChangedFile.cs. `Kind`'s exact wire encoding (the FileChangeKind
 * enum's string name vs. its numeric ordinal) was never exercised by the interop spike — none of
 * the three Stream Deck actions read ChangedFiles, so this is left unconfirmed-but-unused rather
 * than guessed at. Don't trust `Kind`'s type without checking a real payload first.
 */
export interface ChangedFile {
  Path: string;
  Kind: number;
  IsUntracked: boolean;
}

/** Mirrors GitDeck.Git/Repositories/BranchInfo.cs's RepositoryOverview record. */
export interface RepositoryOverview {
  IsRepository: boolean;
  WorkingDirectory: string | null;
  Head: string | null;
  ChangedFiles: ChangedFile[];
  Branches: BranchInfo[];
  LoadError: string | null;
  HasUpstream: boolean;
  AheadBy: number;
  BehindBy: number;
  ChangedFileCount: number;
}

/** Mirrors GitDeck.Git/Repositories/BranchOperations.cs's FetchResult record. */
export interface FetchResult {
  IsDone: boolean;
  ErrorMessage: string | null;
}

/** Mirrors GitDeck.Git/Repositories/BranchOperations.cs's PullResult record. */
export interface PullResult {
  IsPulled: boolean;
  ErrorMessage: string | null;
}

/** Mirrors GitDeck.Git/Repositories/BranchOperations.cs's PushResult record. */
export interface PushResult {
  IsPushed: boolean;
  DidPublish: boolean;
  ErrorMessage: string | null;
}
