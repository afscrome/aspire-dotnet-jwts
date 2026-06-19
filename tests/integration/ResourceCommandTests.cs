namespace aspirifriday.Tests;

#pragma warning disable ASPIREINTERACTION001

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

    [Test]
    public async Task GenerateJwtCommandUsesConfiguredDefaultsWhenArgumentsAreNotProvided(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-default-inputs";

        var appHost = DistributedApplicationTestingBuilder.Create();
        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate JWT With Defaults",
                description: "Mints a JWT using default input values when no arguments are supplied.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("default-user", UserConfigurable: true),
                    ["type"] = new("user", UserConfigurable: false)
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();
        await Assert.That(commandResult.Data).IsNotNull();

        var jwt = JwtHelpers.ReadJwtToken(commandResult.Data!.Value);
        await JwtHelpers.AssertFullClaimSet(
            jwt,
            new()
            {
                ["iss"] = signingKey.Resource.Issuer,
                ["sub"] = "default-user",
                ["type"] = "user"
            });
    }

    [Test]
    public async Task GenerateJwtCommandOverridesConfigurableClaimFromArguments(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-input-override";

        var appHost = DistributedApplicationTestingBuilder.Create();
        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate JWT With Input Override",
                description: "Mints a JWT using submitted command input values for configurable claims.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("default-user", UserConfigurable: true),
                    ["type"] = new("user", UserConfigurable: false)
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var arguments = new InteractionInputCollection(
        [
            new InteractionInput
            {
                Name = "sub",
                InputType = InputType.Text,
                Value = "override-user"
            }
        ]);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, arguments, cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();
        await Assert.That(commandResult.Data).IsNotNull();

        var jwt = JwtHelpers.ReadJwtToken(commandResult.Data!.Value);
        await JwtHelpers.AssertFullClaimSet(
            jwt,
            new()
            {
                ["iss"] = signingKey.Resource.Issuer,
                ["sub"] = "override-user",
                ["type"] = "user"
            });
    }

    [Test]
    public async Task GenerateJwtCommandFailsWhenRequiredConfigurableClaimHasNoValue(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-missing-required-input";

        var appHost = DistributedApplicationTestingBuilder.Create();
        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate JWT Missing Required Input",
                description: "Fails when a required configurable claim has no value.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("", UserConfigurable: true),
                    ["type"] = new("user")
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, cancellationToken);

        await Assert.That(commandResult.Success).IsFalse();
        await Assert.That(commandResult.Data).IsNull();
        await Assert.That(commandResult.Message).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task GenerateJwtCommandFailsWhenRequiredConfigurableClaimIsExplicitlyEmpty(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-empty-required-input";

        var appHost = DistributedApplicationTestingBuilder.Create();
        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate JWT Empty Required Input",
                description: "Fails when a required configurable claim is submitted as empty.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("default-user", UserConfigurable: true),
                    ["type"] = new("user")
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var arguments = new InteractionInputCollection(
        [
            new InteractionInput
            {
                Name = "sub",
                InputType = InputType.Text,
                Value = string.Empty
            }
        ]);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, arguments, cancellationToken);

        await Assert.That(commandResult.Success).IsFalse();
        await Assert.That(commandResult.Data).IsNull();
        await Assert.That(commandResult.Message).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task GenerateJwtCommandWithInputOverridePreservesSignatureMetadata(CancellationToken cancellationToken)
    {
        const string commandName = "generate-jwt-signature-input-override";

        var appHost = DistributedApplicationTestingBuilder.Create();
        var signingKey = appHost.AddJwtSigningToken("signing-key");

        appHost.AddExecutable("fake", "fake", ".")
            .WithExplicitStart()
            .WithJwtToken(
                signingKey,
                commandName: commandName,
                displayName: "Generate Signature Check JWT With Inputs",
                description: "Mints a signed JWT and validates signature metadata with input overrides.",
                additionalClaims: new Dictionary<string, JwtClaimDefault>
                {
                    ["sub"] = new("default-user", UserConfigurable: true),
                    ["type"] = new("user")
                });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        var arguments = new InteractionInputCollection(
        [
            new InteractionInput
            {
                Name = "sub",
                InputType = InputType.Text,
                Value = "override-user"
            }
        ]);

        var commandResult = await app.ResourceCommands
            .ExecuteCommandAsync("fake", commandName, arguments, cancellationToken);

        await Assert.That(commandResult.Success).IsTrue();
        await Assert.That(commandResult.Data).IsNotNull();
        await Assert.That(commandResult.Data!.Value).IsNotNull().And.IsNotEmpty();

        var token = commandResult.Data.Value;

        var signingKeyBase64 = await signingKey.Resource.SigningKeyParameter.GetValueAsync(cancellationToken);
        await Assert.That(signingKeyBase64).IsNotNull().And.IsNotEmpty();

        var keyBytes = Convert.FromBase64String(signingKeyBase64);
        var validationKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes)
        {
            KeyId = signingKey.Resource.KeyId
        };

        var validator = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var validationResult = await validator.ValidateTokenAsync(
            token,
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
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
                ["sub"] = "override-user",
                ["type"] = "user"
            });
    }
}

#pragma warning restore ASPIREINTERACTION001
