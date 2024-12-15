using Microsoft.Extensions.Logging;

namespace Bitbound.SimpleIpc;

public interface ICallbackStoreFactory
{
  ICallbackStore Create();
}

public class CallbackStoreFactory(ILoggerFactory loggerFactory) : ICallbackStoreFactory
{
  private readonly ILoggerFactory _loggerFactory = loggerFactory;

  public ICallbackStore Create()
  {
    var logger = _loggerFactory.CreateLogger<CallbackStore>();
    return new CallbackStore(logger);
  }
}