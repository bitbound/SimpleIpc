using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Bitbound.SimpleIpc;

public interface IIpcRouter
{
  /// <summary>
  /// Creates a message-based IIpcServer that handle messages via registered callbacks.
  /// Message callbacks can be registered using IIpcServer.On method.  The IpcServer
  /// will be assigned a randomly-generated ID for the pipe name.
  /// </summary>
  /// <returns>The newly-created IIpcServer.</returns>
  Task<IIpcServer> CreateServer();

  /// <summary>
  /// Creates a message-based IIpcServer that handle messages via registered callbacks.
  /// Message callbacks can be registered using IIpcServer.On method.
  /// </summary>
  /// <param name="pipeName">The pipe name to use for the IpcServer.</param>
  /// <returns></returns>
  Task<IIpcServer> CreateServer(string pipeName);

  /// <summary>
  /// Creates a message-based IIpcServer that handle messages via registered callbacks.
  /// Message callbacks can be registered using IIpcServer.On method.
  /// </summary>
  /// <param name="pipeName">The pipe name to use for the IpcServer.</param>
  /// <param name="pipeSecurity">Security attributes to add to the pipe server.</param>
  /// <returns></returns>
  Task<IIpcServer> CreateServer(string pipeName, PipeSecurity pipeSecurity);

  bool TryGetServer(string pipeName, [NotNullWhen(true)] out IIpcServer? server);

  bool TryRemoveServer(string pipeName, [NotNullWhen(true)] out IIpcServer? server);
}

public class IpcRouter(IIpcConnectionFactory serverFactory, ILogger<IpcRouter> logger) : IIpcRouter
{
  private static readonly ConcurrentDictionary<string, IIpcServer> _pipeStreams = new();
  private static IpcRouter? _default;
  private readonly ILogger<IpcRouter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  private readonly IIpcConnectionFactory _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));

  public static IIpcRouter Default => _default ??= new IpcRouter(IpcConnectionFactory.Default, new LoggerFactory().CreateLogger<IpcRouter>());

  public async Task<IIpcServer> CreateServer()
  {
    var pipeName = Guid.NewGuid().ToString();
    return await CreateServerInternal(pipeName);
  }

  public async Task<IIpcServer> CreateServer(string pipeName)
  {
    return await CreateServerInternal(pipeName);
  }

  [SupportedOSPlatform("windows")]
  public async Task<IIpcServer> CreateServer(string pipeName, PipeSecurity pipeSecurity)
  {
    return await CreateServerInternal(pipeName, pipeSecurity);
  }

  public bool TryGetServer(string pipeName, [NotNullWhen(true)] out IIpcServer? server)
  {
    return _pipeStreams.TryGetValue(pipeName, out server);
  }

  public bool TryRemoveServer(string pipeName, [NotNullWhen(true)] out IIpcServer? server)
  {
    return _pipeStreams.TryRemove(pipeName, out server);
  }

  private async Task<IIpcServer> CreateServerInternal(string pipeName)
  {
    if (string.IsNullOrWhiteSpace(pipeName))
    {
      throw new ArgumentNullException(nameof(pipeName));
    }

    _logger.LogDebug("Creating pipe message server {name}.", pipeName);

    var serverConnection = await _serverFactory.CreateServer(pipeName);

    serverConnection.ReadingEnded += ServerConnection_ReadingEnded;

    if (!_pipeStreams.TryAdd(pipeName, serverConnection))
    {
      throw new ArgumentException("The pipe name is already in use.");
    }

    return serverConnection;
  }

  [SupportedOSPlatform("windows")]
  private async Task<IIpcServer> CreateServerInternal(string pipeName, PipeSecurity pipeSecurity)
  {
    if (string.IsNullOrWhiteSpace(pipeName))
    {
      throw new ArgumentNullException(nameof(pipeName));
    }

    _logger.LogDebug("Creating pipe message server {name}.", pipeName);

    var serverConnection = await _serverFactory.CreateServer(pipeName, pipeSecurity);

    serverConnection.ReadingEnded += ServerConnection_ReadingEnded;

    if (!_pipeStreams.TryAdd(pipeName, serverConnection))
    {
      throw new ArgumentException("The pipe name is already in use.");
    }

    return serverConnection;
  }

  private void ServerConnection_ReadingEnded(object? sender, IConnectionBase args)
  {
    if (_pipeStreams.TryRemove(args.PipeName, out var server))
    {
      server.ReadingEnded -= ServerConnection_ReadingEnded;
    }
    else
    {
      _logger.LogWarning("Pipe name {pipeName} not found.", args.PipeName);
    }
  }
}