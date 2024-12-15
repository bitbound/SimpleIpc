using Microsoft.Extensions.DependencyInjection;

namespace Bitbound.SimpleIpc;

public static class IServiceCollectionExtensions
{
  public static IServiceCollection AddSimpleIpc(this IServiceCollection services)
  {
    services.AddLogging();
    services.AddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
    services.AddSingleton<ICallbackStoreFactory, CallbackStoreFactory>();
    services.AddSingleton<IIpcRouter, IpcRouter>();
    services.AddTransient<ICallbackStore, CallbackStore>();
    return services;
  }
}