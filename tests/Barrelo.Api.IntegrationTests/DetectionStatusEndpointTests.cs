using System.Net.Http.Json;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Barrelo.Api.IntegrationTests;

/// <summary>
/// The board pill used to be a hardcoded "Manual entry — no board connected" on control.html, which was a
/// lie on every host actually wired to a board. This is the endpoint it reads instead on load; every
/// change after that arrives as a DetectionStatusChanged push on the game hub.
/// </summary>
public sealed class DetectionStatusEndpointTests(BarreloApiFactory factory) : IClassFixture<BarreloApiFactory>
{
    [Fact]
    public async Task A_host_with_no_board_reports_the_mock_source_as_available()
    {
        var status = await GetStatus(("Detection:Mode", "Mock"));

        // Nothing to connect to, so nothing can drop. The pill reads the source, not the flag, to decide
        // this is manual entry rather than a board that is down.
        status!.Source.Should().Be(DetectionSourceType.Mock);
        status.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task A_board_that_cannot_be_reached_reports_the_configured_source_as_disconnected()
    {
        // Port 1 has nothing on it, so this is the state a host boots into whenever the board manager
        // isn't up yet — which is exactly what the pill has to be able to say out loud.
        var status = await GetStatus(
            ("Detection:Mode", "AutoDarts"),
            ("Detection:AutoDarts:Url", "ws://127.0.0.1:1/api/events"));

        status!.Source.Should().Be(DetectionSourceType.AutoDarts);
        status.IsConnected.Should().BeFalse();
    }

    private async Task<DetectionStatus?> GetStatus(params (string Key, string Value)[] settings)
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        });

        var client = configured.CreateClient();
        return await client.GetFromJsonAsync<DetectionStatus>("/api/detection/status", JsonTestOptions.Options);
    }
}
