using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREINTERACTION001

namespace Aspire.Hosting;

public static class JwtResourceBuilderExtensions
{
    /// <summary>
    /// Configures the resource with JWT validation settings and adds a single
    /// command that mints a signed bearer token on demand.
    /// </summary>
    public static IResourceBuilder<T> WithJwtToken<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<SigningTokenResource> signingToken,
        string commandName,
        string displayName,
        string description,
        IDictionary<string, JwtClaimDefault>? additionalClaims = null)
        where T : IResourceWithEnvironment
    {
        // Inject the signing key as configuration so the service can validate tokens.
        builder
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Id", signingToken.Resource.KeyId)
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Issuer", signingToken.Resource.Issuer)
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Value", signingToken.Resource.SigningKeyParameter)
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Length", "32")
            .WithEnvironment("Authentication__Schemes__Bearer__ValidIssuers__0", signingToken.Resource.Issuer);

        var configuredClaims = additionalClaims ?? new Dictionary<string, JwtClaimDefault>(StringComparer.Ordinal);

        // Declare inputs for user-configurable claims so the dashboard prompts before execution.
        var configurableInputs = configuredClaims
            .Where(x => x.Value.UserConfigurable)
            .Select(x => new InteractionInput
            {
                Name = x.Key,
                Label = x.Value.Label ?? x.Key,
                Description = x.Value.Description,
                InputType = InputType.Text,
                Value = x.Value.Value,
                Required = true
            })
            .ToArray();

        builder.WithCommand(
            name: commandName,
            displayName: displayName,
            executeCommand: async context =>
            {
                // Start with all defaults so non-configurable claims are always included.
                var claims = configuredClaims
                    .ToDictionary(x => x.Key, x => (object)x.Value.Value, StringComparer.Ordinal);

                // Override with values submitted by the user for configurable claims.
                foreach (var input in configurableInputs)
                {
                    claims[input.Name] = context.Arguments.GetString(input.Name) ?? input.Value ?? string.Empty;
                }

                var token = await signingToken.Resource.GenerateJwtAsync(
                    claims,
                    context.CancellationToken);

                return new ExecuteCommandResult
                {
                    Success = true,
                    Message = "JWT generated - copy the token below and use it as a Bearer header.",
                    Data = new()
                    {
                        Value = token,
                        DisplayImmediately = true
                    }
                };
            },
            commandOptions: new CommandOptions
            {
                Description = description,
                IconName = "Key",
                IconVariant = IconVariant.Regular,
                Arguments = configurableInputs,
            });

        return builder;
    }
}

#pragma warning restore ASPIREINTERACTION001
