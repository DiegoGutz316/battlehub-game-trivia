using System.Net;

namespace BattleHub.Trivia.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(TriviaDatabaseCollection.Name)]
public class HealthCheckTests(TriviaApiFactory factory)
{
    [Fact]
    public async Task GetHealth_RetornaOkYHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "Healthy",
            await response.Content.ReadAsStringAsync());
    }
}
