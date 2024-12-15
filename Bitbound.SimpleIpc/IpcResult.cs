#nullable disable

using Bitbound.SimpleIpc;
using System;

namespace Bitbound.SimpleIpc;

public class IpcResult(bool isSuccess, string error = null, Exception exception = null)
{
  public static IpcResult<T> Empty<T>()
  {
    return new IpcResult<T>(true, default);
  }

  public static IpcResult Fail(string error)
  {
    return new IpcResult(false, error);
  }

  public static IpcResult Fail(Exception ex)
  {
    return new IpcResult(false, null, ex);
  }

  public static IpcResult Fail(Exception ex, string error)
  {
    return new IpcResult(false, error, ex);
  }

  public static IpcResult<T> Fail<T>(string error)
  {
    return new IpcResult<T>(false, default, error);
  }

  public static IpcResult<T> Fail<T>(Exception ex)
  {
    return new IpcResult<T>(false, default, exception: ex);
  }

  public static IpcResult<T> Fail<T>(Exception ex, string message)
  {
    return new IpcResult<T>(false, default, message, ex);
  }

  public static IpcResult Ok()
  {
    return new IpcResult(true);
  }

  public static IpcResult<T> Ok<T>(T value)
  {
    return new IpcResult<T>(true, value, null);
  }

  public bool IsSuccess { get; private set; } = isSuccess;

  public string Error { get; private set; } = error;

  public Exception Exception { get; private set; } = exception;
}

public class IpcResult<T>(bool isSuccess, T value, string error = null, Exception exception = null)
{
  public bool IsSuccess { get; private set; } = isSuccess;

  public string Error { get; private set; } = error;

  public Exception Exception { get; private set; } = exception;

  public T Value { get; private set; } = value;
}