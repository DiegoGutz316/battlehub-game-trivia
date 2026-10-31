using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleHub.Trivia.IntegrationTests;

// R-09: la API no arranca con valores escritos en el código si falta la configuración.
public sealed class RequiredConfigurationTests
{
    private static Exception? StartWith(params (string Key, string Value)[] settings)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

        return Record.Exception(() => factory.CreateClient());
    }

    [Fact]
    public void FailsWithoutConnectionString()
    {
        var error = StartWith(("Cors:Origins:0", "http://localhost:4000"));

        Assert.Contains("ConnectionStrings__Trivia", error?.Message);
    }

    [Fact]
    public void FailsWithoutCorsOrigins()
    {
        var error = StartWith(("ConnectionStrings:Trivia", "Server=sin-usar"));

        Assert.Contains("Cors__Origins__0", error?.Message);
    }
}
