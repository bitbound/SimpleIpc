﻿using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Bitbound.SimpleIpc;

public interface IIpcServer : IConnectionBase
{
  Task<bool> WaitForConnection(CancellationToken cancellationToken);
}

internal class IpcServer : ConnectionBase, IIpcServer
{
  public IpcServer(
      string pipeName,
      ICallbackStoreFactory callbackFactory,
      ILogger<IpcServer> logger)
      : base(pipeName, callbackFactory, logger)
  {
    _pipeStream = new NamedPipeServerStream(
        pipeName,
        PipeDirection.InOut,
        1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);
  }

  [SupportedOSPlatform("windows")]
  public IpcServer(
      string pipeName,
      PipeSecurity pipeSecurity,
      ICallbackStoreFactory callbackFactory,
      ILogger<IpcServer> logger)
      : base(pipeName, callbackFactory, logger)
  {
    _pipeStream = NamedPipeServerStreamAcl.Create(
        pipeName,
        PipeDirection.InOut,
        1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous,
        0,
        0,
        pipeSecurity);
  }

  public async Task<bool> WaitForConnection(CancellationToken cancellationToken)
  {
    try
    {
      await _connectLock.WaitAsync();

      if (_pipeStream is null)
      {
        throw new InvalidOperationException($"You must initialize the connection before calling this method.");
      }

      if (_pipeStream is NamedPipeServerStream serverStream)
      {
        await serverStream.WaitForConnectionAsync(cancellationToken);
        _logger.LogDebug("Connection established for server pipe {id}.", PipeName);
      }
      else
      {
        throw new InvalidOperationException($"{nameof(_pipeStream)} is not of type NamedPipeServerStream.");
      }

      if (!_pipeStream.IsConnected)
      {
        return false;
      }

      return true;
    }
    catch (TaskCanceledException)
    {
      return false;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    finally
    {
      _connectLock.Release();
    }
  }
}