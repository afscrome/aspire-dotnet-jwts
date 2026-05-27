using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace aspirifriday.Tests;

[Timeout(30_000)]
public class DotnetUserJwtConfigurationTests
{
    // Missing test coverage around user configurable claims
    // Requires mocking out IInteractionService
    // But will be redundant in 13.4 once
    
    [Test]
    public async Task SigningKeyResourceGenerateJwtProducesValidSignature(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-signature-check";

        var appHost = DistributedApplicationTestingBuilder.Create();

        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate Signature Check JWT",
                description: "Mints a signed JWT for signature validation checks.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("dev-user"),
                    ["type"] = new("user")
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();
        await Assert.That(commandResult.Data).IsNotNull();
        await Assert.That(commandResult.Data!.Value).IsNotNull().And.IsNotEmpty();

        var token = commandResult.Data.Value;

        var signingKeyBase64 = await signingKey.Resource.SigningKeyParameter.GetValueAsync(cancellationToken);
        await Assert.That(signingKeyBase64).IsNotNull().And.IsNotEmpty();

        var keyBytes = Convert.FromBase64String(signingKeyBase64);
        var validationKey = new SymmetricSecurityKey(keyBytes) { KeyId = signingKey.Resource.KeyId };

        var validator = new JsonWebTokenHandler();
        var validationResult = await validator.ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = validationKey,
                ValidateIssuer = true,
                ValidIssuer = signingKey.Resource.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            });

        await Assert.That(validationResult.IsValid).IsTrue();
        await Assert.That(validationResult.Exception).IsNull();

        var jwt = JwtHelpers.ReadJwtToken(token);
        await Assert.That(jwt.Issuer).IsEqualTo(signingKey.Resource.Issuer);
        await Assert.That(jwt.Kid).IsEqualTo(signingKey.Resource.KeyId);
        await JwtHelpers.AssertFullClaimSet(
            jwt,
            new()
            {
                ["iss"] = signingKey.Resource.Issuer,
                ["sub"] = "dev-user",
                ["type"] = "user"
            });
    }
}
