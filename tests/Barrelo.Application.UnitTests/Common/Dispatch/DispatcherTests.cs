using Barrelo.Application.Common.Dispatch;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Barrelo.Application.UnitTests.Common.Dispatch;

public class DispatcherTests
{
    private sealed record Ping(string Text) : IRequest<string>;

    private sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken ct) => Task.FromResult($"pong: {request.Text}");
    }

    private sealed record UnregisteredRequest : IRequest<string>;

    private sealed record FailingRequest : IRequest<ErrorOr<int>>;

    private sealed class FailingRequestHandler : IRequestHandler<FailingRequest, ErrorOr<int>>
    {
        public Task<ErrorOr<int>> Handle(FailingRequest request, CancellationToken ct) =>
            Task.FromResult<ErrorOr<int>>(Error.Validation("Test.Failure", "always fails"));
    }

    private sealed record Announced(List<string> Log) : INotification;

    private sealed class FirstAnnouncedHandler : INotificationHandler<Announced>
    {
        public Task Handle(Announced notification, CancellationToken ct)
        {
            notification.Log.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondAnnouncedHandler : INotificationHandler<Announced>
    {
        public Task Handle(Announced notification, CancellationToken ct)
        {
            notification.Log.Add("second");
            return Task.CompletedTask;
        }
    }

    private static IDispatcher BuildDispatcher(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task Send_routes_to_the_registered_handler()
    {
        var dispatcher = BuildDispatcher(s => s.AddScoped<IRequestHandler<Ping, string>, PingHandler>());

        var result = await dispatcher.Send(new Ping("hello"), CancellationToken.None);

        result.Should().Be("pong: hello");
    }

    [Fact]
    public async Task Send_with_no_registered_handler_throws_a_clear_error()
    {
        var dispatcher = BuildDispatcher();

        var act = () => dispatcher.Send(new UnregisteredRequest(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{nameof(UnregisteredRequest)}*");
    }

    [Fact]
    public async Task Send_with_multiple_registered_handlers_throws_a_clear_error()
    {
        var dispatcher = BuildDispatcher(s =>
        {
            s.AddScoped<IRequestHandler<Ping, string>, PingHandler>();
            s.AddScoped<IRequestHandler<Ping, string>, PingHandler>();
        });

        var act = () => dispatcher.Send(new Ping("hello"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Send_returns_ErrorOr_failure_unchanged()
    {
        var dispatcher = BuildDispatcher(s => s.AddScoped<IRequestHandler<FailingRequest, ErrorOr<int>>, FailingRequestHandler>());

        var result = await dispatcher.Send(new FailingRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Test.Failure");
    }

    [Fact]
    public async Task Publish_with_no_handlers_is_a_silent_no_op()
    {
        var dispatcher = BuildDispatcher();

        var act = () => dispatcher.Publish(new Announced([]), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_reaches_all_registered_handlers()
    {
        var dispatcher = BuildDispatcher(s =>
        {
            s.AddScoped<INotificationHandler<Announced>, FirstAnnouncedHandler>();
            s.AddScoped<INotificationHandler<Announced>, SecondAnnouncedHandler>();
        });
        var log = new List<string>();

        await dispatcher.Publish(new Announced(log), CancellationToken.None);

        log.Should().BeEquivalentTo(["first", "second"]);
    }
}
