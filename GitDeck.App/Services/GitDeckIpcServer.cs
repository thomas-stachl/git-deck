using System.IO.Pipes;
using GitDeck.Ipc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StreamJsonRpc;

namespace GitDeck.App.Services;

/// <summary>
/// Listens on a named pipe for an out-of-process client (the Stream Deck plugin) and exposes
/// <see cref="IGitDeckIpc"/> to it over StreamJsonRpc.
/// </summary>
/// <remarks>
/// There is no <c>IHostedService</c>/generic <c>Host</c> anywhere in this app to start this on a
/// "run until shutdown" lifecycle, so this follows the same shape
/// <see cref="WindowsGlobalHotkeyService"/> already uses: the constructor starts the background work
/// itself, and disposal (the <c>ServiceProvider</c> teardown <c>Program.Main</c> already runs via
/// <c>using var services = ...</c>) stops it.
/// </remarks>
public sealed class GitDeckIpcServer : IDisposable
{
    private readonly IGitDeckIpc _target;
    private readonly string _pipeName;
    private readonly ILogger<GitDeckIpcServer> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private bool _isDisposed;

    public GitDeckIpcServer(IGitDeckIpc target, ILogger<GitDeckIpcServer>? logger = null)
        : this(target, GitDeckIpcConstants.PipeName, logger)
    {
    }

    /// <param name="pipeName">
    /// Trailing, not DI-resolved: exists so tests can point at a throwaway pipe name instead of the
    /// production one, never so production code overrides it.
    /// </param>
    public GitDeckIpcServer(IGitDeckIpc target, string pipeName, ILogger<GitDeckIpcServer>? logger = null)
    {
        _target = target;
        _pipeName = pipeName;
        _logger = logger ?? NullLogger<GitDeckIpcServer>.Instance;

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _cts.Cancel();

        try
        {
            _acceptLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Expected: the loop observes the cancellation via OperationCanceledException.
        }

        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;

            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not create the {PipeName} named pipe.", _pipeName);
                return;
            }

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "The {PipeName} named pipe stopped accepting connections.", _pipeName);
                pipe.Dispose();
                continue;
            }

            // Fire-and-forget: a client connection is handled independently of the accept loop, so a
            // slow or long-lived client (a Stream Deck plugin left open) never blocks the next one
            // (a plugin restart) from connecting.
            _ = HandleClientAsync(pipe, cancellationToken);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            // Explicit, rather than JsonRpc.Attach's implicit default: the wire framing (a
            // Content-Length-prefixed JSON body, the same style the Language Server Protocol uses)
            // needs to be pinned and documented, because a non-.NET client has no compiler-shared
            // contract to fall back on if a future StreamJsonRpc version changes its default.
            var handler = new HeaderDelimitedMessageHandler(pipe, pipe, new SystemTextJsonFormatter());

            using var rpc = new JsonRpc(handler, _target);
            using var registration = cancellationToken.Register(rpc.Dispose);

            rpc.StartListening();
            await rpc.Completion;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "A GitDeck.Ipc client connection ended unexpectedly.");
        }
        finally
        {
            pipe.Dispose();
        }
    }
}
