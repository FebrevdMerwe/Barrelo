using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Barrelo.Application.Common.Dispatch;

public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), (Type HandlerType, MethodInfo HandleMethod)> RequestHandlerCache = new();
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo HandleMethod)> NotificationHandlerCache = new();

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
    {
        var key = (request.GetType(), typeof(TResponse));
        var (handlerType, handleMethod) = RequestHandlerCache.GetOrAdd(key, static k =>
        {
            var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(k.RequestType, k.ResponseType);
            return (handlerInterface, handlerInterface.GetMethod(nameof(IRequestHandler<IRequest<object>, object>.Handle))!);
        });

        var handlers = serviceProvider.GetServices(handlerType).ToArray();

        if (handlers.Length == 0)
            throw new InvalidOperationException($"No handler is registered for request type '{key.Item1.Name}'.");

        if (handlers.Length > 1)
            throw new InvalidOperationException($"Multiple handlers are registered for request type '{key.Item1.Name}'; exactly one is required.");

        return await (Task<TResponse>)handleMethod.Invoke(handlers[0], [request, ct])!;
    }

    public async Task Publish(INotification notification, CancellationToken ct)
    {
        var notificationType = notification.GetType();
        var (handlerType, handleMethod) = NotificationHandlerCache.GetOrAdd(notificationType, static t =>
        {
            var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(t);
            return (handlerInterface, handlerInterface.GetMethod(nameof(INotificationHandler<INotification>.Handle))!);
        });

        var handlers = serviceProvider.GetServices(handlerType).ToArray();
        if (handlers.Length == 0)
            return; // no-op: Phase 1 has no GameStateChangedEvent consumers yet, and that must not be an error

        List<Exception>? exceptions = null;
        foreach (var handler in handlers)
        {
            try
            {
                await (Task)handleMethod.Invoke(handler, [notification, ct])!;
            }
            catch (Exception ex)
            {
                (exceptions ??= []).Add(ex);
            }
        }

        if (exceptions is not null)
            throw new AggregateException($"One or more handlers for notification '{notificationType.Name}' failed.", exceptions);
    }
}
