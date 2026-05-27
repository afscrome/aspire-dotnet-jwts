namespace aspirifriday.Tests;

[Timeout(30_000)]
public class ResourceCommandTests
{
    [Test]
    public async Task GenerateJwtCommandReturnsTokenWithExpectedMetadata(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-user";

        var appHost = DistributedApplicationTestingBuilder.Create();

        var signingKey = appHost.AddJwtSigningToken("signing-key")
            .WithDefaultClaim("sub", "dev-user")
            .WithDefaultClaim("type", "user");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate User JWT",
                description: "Mints a signed user JWT bearer token using the configured static signing key.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("dev-user", UserConfigurable: false),
                    ["type"] = new("user")
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();
        await Assert.That(commandResult.Data).IsNotNull();
        await Assert.That(commandResult.Data!.Value).IsNotNull().And.IsNotEmpty();

        var jwt = JwtHelpers.ReadJwtToken(commandResult.Data.Value);
        await Assert.That(jwt.Issuer).IsEqualTo(signingKey.Resource.Issuer);
        await Assert.That(jwt.Kid).IsEqualTo(signingKey.Resource.Name);
        await JwtHelpers.AssertFullClaimSet(
            jwt,
            new()
            {
                ["iss"] = signingKey.Resource.Issuer,
                ["sub"] = "dev-user",
                ["type"] = "user"
            });
    }

    [Test]
    public async Task GenerateServiceJwtCommandReturnsTokenWithExpectedMetadata(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-service";

        var appHost = DistributedApplicationTestingBuilder.Create();

        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate Service JWT",
                description: "Mints a signed service JWT bearer token using the configured static signing key.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["aud"] = new("dotnetappwithauth"),
                    ["sub"] = new("dev-service", UserConfigurable: false),
                    ["type"] = new("service")
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();
        await Assert.That(commandResult.Data).IsNotNull();
        await Assert.That(commandResult.Data!.Value).IsNotNull().And.IsNotEmpty();

        var jwt = JwtHelpers.ReadJwtToken(commandResult.Data.Value);
        await Assert.That(jwt.Issuer).IsEqualTo(signingKey.Resource.Issuer);
        await Assert.That(jwt.Kid).IsEqualTo(signingKey.Resource.Name);
        await JwtHelpers.AssertFullClaimSet(
            jwt,
            new()
            {
                ["iss"] = signingKey.Resource.Issuer,
                ["aud"] = "dotnetappwithauth",
                ["sub"] = "dev-service",
                ["type"] = "service"
            });
    }

}
