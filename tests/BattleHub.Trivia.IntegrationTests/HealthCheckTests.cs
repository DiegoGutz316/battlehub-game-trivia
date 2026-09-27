using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleHub.Trivia.IntegrationTests;

[Trait("Category", "Integration")]
public class HealthCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_RetornaOkYHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
