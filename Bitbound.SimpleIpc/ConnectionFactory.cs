﻿using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Bitbound.SimpleIpc;

public interface IIpcConnectionFactory
{
  Task<IIpcClient> CreateClient(string serverName, string pipeName);

  Task<IIpcServer> CreateServer(string pipeName);

  [SupportedOSPlatform("windows")]
  Task<IIpcServer> CreateServer(string pipeName, PipeSecurity pipeSecurity);
}

public class IpcConnectionFactory(ICallbackStoreFactory callbackFactory, ILoggerFactory loggerFactory) : IIpcConnectionFactory
{
  private static IIpcConnectionFactory? _default;
  private readonly ICallbackStoreFactory _callbackFactory = callbackFactory;
  private readonly ILoggerFactory _loggerFactory = loggerFactory;

  public static IIpcConnectionFactory Default =>
      _default ??=
      new IpcConnectionFactory(new CallbackStoreFactory(new LoggerFactory()), new LoggerFactory());

  public Task<IIpcClient> CreateClient(string serverName, string pipeName)
  {
    var client = new IpcClient(
        serverName,
        pipeName,
        _callbackFactory,
        _loggerFactory.CreateLogger<IpcClient>());
    return Task.FromResult((IIpcClient)client);
  }

  public Task<IIpcServer> CreateServer(string pipeName)
  {
    var server = new IpcServer(
        pipeName,
        _callbackFactory,
        _loggerFactory.CreateLogger<IpcServer>());
    return Task.FromResult((IIpcServer)server);
  }

  [SupportedOSPlatform("windows")]
  public Task<IIpcServer> CreateServer(string pipeName, PipeSecurity pipeSecurity)
  {
    var server = new IpcServer(
        pipeName,
        pipeSecurity,
        _callbackFactory,
        _loggerFactory.CreateLogger<IpcServer>());
    return Task.FromResult((IIpcServer)server);
  }
}