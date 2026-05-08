using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace aspirefriday.Tests;

public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task GenerateJwtAndCallClaimsEndpointReturnsExpectedClaims()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;

        var appHost = DistributedApplicationTestingBuilder.Create();

        var signingKey = appHost.AddJwtSigningToken(
            name: "signing-key",
            issuer: "dotnet-user-jwts");

        var apiService = appHost.AddProject<Projects.aspirefriday_ApiService>("apiservice")
            .WithHttpHealthCheck("/health")
            .WithJwtToken(
                signingKey,
                commandName: "generate-jwt",
                displayName: "Generate User JWT",
                description: "Mints a signed user JWT bearer token using the configured static signing key.",
                defaultClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("dev-user", UserConfigurable: true, Label: "User ID", Description: "Value used for the sub claim."),
                    ["type"] = new("user"),
                    ["thing"] = new("1", UserConfigurable: true, Label: "Thing claim")
                });


        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
#pragma warning disable EXTEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            clientBuilder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Wait for the API service to be healthy before calling it
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        // Act — generate a JWT via the resource command
        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("apiservice", "generate-jwt", cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();

        var token = commandResult.Data!.Value;

        var httpClient = app.CreateHttpClient("apiservice");
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.GetAsync("/claims", cancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var claims = await response.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken);

        await Assert.That(claims).IsNotNull();

        // The user JWT minted by generate-jwt sets sub = "dev-user" and type = "user"
        var subClaim = claims!.FirstOrDefault(c =>
            c.GetProperty("type").GetString()!.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase) ||
            c.GetProperty("type").GetString() == "sub");

        var typeClaim = claims.FirstOrDefault(c =>
            string.Equals(c.GetProperty("type").GetString(), "type", StringComparison.Ordinal));

        await Assert.That(subClaim.ValueKind).IsNotEqualTo(JsonValueKind.Undefined);
        await Assert.That(subClaim.GetProperty("value").GetString()).IsEqualTo("dev-user");
        await Assert.That(typeClaim.ValueKind).IsNotEqualTo(JsonValueKind.Undefined);
        await Assert.That(typeClaim.GetProperty("value").GetString()).IsEqualTo("user");
    }

    [Test]
    public async Task GenerateServiceJwtAndCallClaimsEndpointReturnsExpectedClaims()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;

        var appHost = DistributedApplicationTestingBuilder.Create();

        var signingKey = appHost.AddJwtSigningToken(
            name: "signing-key",
            issuer: "dotnet-user-jwts");

        var apiService = appHost.AddProject<Projects.aspirefriday_ApiService>("apiservice")
            .WithHttpHealthCheck("/health")
            .WithJwtToken(
                signingKey,
                commandName: "generate-service-jwt",
                displayName: "Generate Service JWT",
                description: "Mints a signed service JWT bearer token using the configured static signing key.",
                defaultClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("dev-service", UserConfigurable: true, Label: "App ID", Description: "Application identifier used for the sub claim."),
                    ["type"] = new("service"),
                    ["thing"] = new("1", UserConfigurable: true, Label: "Thing claim")
                });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("apiservice", "generate-service-jwt", cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();

        var token = commandResult.Data!.Value;

        var httpClient = app.CreateHttpClient("apiservice");
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.GetAsync("/claims", cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var claims = await response.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken);

        await Assert.That(claims).IsNotNull();

        var subClaim = claims!.FirstOrDefault(c =>
            c.GetProperty("type").GetString()!.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase) ||
            c.GetProperty("type").GetString() == "sub");

        var typeClaim = claims.FirstOrDefault(c =>
            string.Equals(c.GetProperty("type").GetString(), "type", StringComparison.Ordinal));

        await Assert.That(subClaim.ValueKind).IsNotEqualTo(JsonValueKind.Undefined);
        await Assert.That(subClaim.GetProperty("value").GetString()).IsEqualTo("dev-service");
        await Assert.That(typeClaim.ValueKind).IsNotEqualTo(JsonValueKind.Undefined);
        await Assert.That(typeClaim.GetProperty("value").GetString()).IsEqualTo("service");
    }
}
