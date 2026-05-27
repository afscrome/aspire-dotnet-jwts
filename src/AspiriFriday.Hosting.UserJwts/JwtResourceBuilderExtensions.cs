using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

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

        builder.WithCommand(
            name: commandName,
            displayName: displayName,
            executeCommand: async context =>
            {
                var resolvedClaims = await ResolveClaimsAsync(context, additionalClaims);

                if (resolvedClaims.Canceled)
                {
                    return CommandResults.Canceled();
                }

                var token = await signingToken.Resource.GenerateJwtAsync(
                    resolvedClaims.Claims,
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
            });

        return builder;
    }

    private static IReadOnlyDictionary<string, string> MergeClaims(
        IReadOnlyDictionary<string, object> resourceDefaults,
        IReadOnlyDictionary<string, string> commandClaims)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (claimType, claimValue) in resourceDefaults)
        {
            merged[claimType] = claimValue?.ToString() ?? string.Empty;
        }

        foreach (var (claimType, claimValue) in commandClaims)
        {
            merged[claimType] = claimValue;
        }

        return merged;
    }

    private static async Task<(bool Canceled, Dictionary<string, object> Claims)> ResolveClaimsAsync(
        ExecuteCommandContext context,
        IDictionary<string, JwtClaimDefault>? additionalClaims)
    {
        var allConfiguredClaims = additionalClaims?.ToList() ?? [];
        var promptClaims = allConfiguredClaims.Where(x => x.Value.UserConfigurable).ToList();

        // Start with all defaults so non-configurable claims are always included.
        var claims = allConfiguredClaims.ToDictionary(x => x.Key, x => (object)x.Value.Value);

        if (promptClaims.Count > 0)
        {
            var interactionService = context.ServiceProvider.GetRequiredService<IInteractionService>();
            if (!interactionService.IsAvailable)
            {
                throw new InvalidOperationException("Cannot prompt for claim values because the interaction service is not available.");
            }

            var configurableInputs = new List<InteractionInput>();

            foreach (var (claimType, claimDefault) in promptClaims)
            {
                configurableInputs.Add(new InteractionInput
                {
                    Name = claimType,
                    Label = claimDefault.Label ?? claimType,
                    Description = claimDefault.Description,
                    InputType = InputType.Text,
                    Value = claimDefault.Value,
                    Required = true
                });
            }

            var result = await interactionService.PromptInputsAsync(
                title: "Generate JWT",
                message: "Configure claim values",
                inputs: configurableInputs,
                options: null,
                cancellationToken: context.CancellationToken);

            if (result.Canceled || result.Data is null)
            {
                return (true, claims);
            }

            foreach (var input in configurableInputs)
            {
                InteractionInput? configuredInput = null;
                if (result.Data.TryGetByName(input.Name, out configuredInput) && configuredInput is not null)
                {
                    claims[input.Name] = configuredInput.Value ?? string.Empty;
                }
                else
                {
                    claims[input.Name] = input.Value ?? string.Empty;
                }
            }
        }

        return (false, claims);
    }
}
