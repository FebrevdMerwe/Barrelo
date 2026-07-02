namespace Darts.Application.Common.Dispatch;

public interface IDispatcher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct);

    Task Publish(INotification notification, CancellationToken ct);
}
