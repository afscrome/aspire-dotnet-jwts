using Aspire.Hosting.ApplicationModel;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

#pragma warning disable ASPIREINTERACTION001

namespace Aspire.Hosting;

public static class JwtResourceBuilderExtensions
{
    private const string KeyId = "f30343ac";
    private const string Audience = "aspirefriday-api";

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
        IReadOnlyDictionary<string, JwtClaimDefault>? defaultClaims = null)
        where T: IResourceWithEnvironment
    {
        // Inject the signing key as configuration so the service can validate tokens.
        builder
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Id", KeyId)
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Issuer", signingToken.Resource.Issuer)
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Value", signingToken.Resource.SigningKeyParameter)
            .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Length", "32");

        builder.WithCommand(
            name: commandName,
            displayName: displayName,
            executeCommand: async context =>
            {
                var keyValue = await signingToken.Resource.SigningKeyParameter.Resource.GetValueAsync(context.CancellationToken);
                var claimResolutionResult = await ResolveClaimsAsync(context, defaultClaims);

                if (claimResolutionResult.Canceled)
                {
                    return CommandResults.Canceled();
                }

                return await GenerateJwtAsync(keyValue!, signingToken.Resource.Issuer, claimResolutionResult.Claims);
            },
            commandOptions: new CommandOptions
            {
                Description = description,
                IconName = "Key",
                IconVariant = IconVariant.Regular,
            });

        return builder;
    }

    public static IResourceBuilder<SigningTokenResource> AddJwtSigningToken(
        this IDistributedApplicationBuilder builder,
        string name,
        string issuer)
    {
        var jwtSigningKey = builder.AddParameter(
            "jwt-signing-key",
            new Base64ParameterDefault(32),
            secret: true,
            persist: true);

        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(jwtSigningKey);

        var resource = new SigningTokenResource(name, issuer, jwtSigningKey);
        var state = new CustomResourceSnapshot
        {
            ResourceType = "SigningToken",
            Properties =
            [
                new("issuer", issuer)
            ],
            State = KnownResourceStates.Active
        };

        return builder.AddResource(resource)
            .WithInitialState(state)
            .WithIconName("Certificate");
    }

    private static async Task<(bool Canceled, IReadOnlyDictionary<string, string> Claims)> ResolveClaimsAsync(
        ExecuteCommandContext context,
        IReadOnlyDictionary<string, JwtClaimDefault>? defaultClaims)
    {
        var claims = new Dictionary<string, string>(StringComparer.Ordinal);

        var configurableInputs = new List<InteractionInput>();

        if (defaultClaims is not null)
        {
            foreach (var (claimType, claimDefault) in defaultClaims)
            {
                if (claimDefault.UserConfigurable)
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
                else
                {
                    claims[claimType] = claimDefault.Value;
                }
            }
        }

        if (context.ServiceProvider.GetService(typeof(IInteractionService)) is not IInteractionService interactionService ||
            !interactionService.IsAvailable)
        {
            foreach (var input in configurableInputs)
            {
                claims[input.Name] = input.Value ?? string.Empty;
            }

            return (false, claims);
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

        return (false, claims);
    }

    private static Task<ExecuteCommandResult> GenerateJwtAsync(
        string signingKeyBase64,
        string issuer,
        IReadOnlyDictionary<string, string> claims)
    {
        var keyBytes = Convert.FromBase64String(signingKeyBase64);
        var securityKey = new SymmetricSecurityKey(keyBytes) { KeyId = KeyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenClaims = claims.Select(kvp => new Claim(kvp.Key, kvp.Value));

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: Audience,
            claims: tokenClaims,

            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var result = CommandResults.Success(
            message: "JWT generated — copy the token below and use it as a Bearer header.",
            result: tokenString);
        return Task.FromResult(result);
    }
}

#pragma warning restore ASPIREINTERACTION001

internal sealed class Base64ParameterDefault(int byteLength) : Aspire.Hosting.ApplicationModel.ParameterDefault
{
    public int ByteLength { get; } = byteLength > 0
        ? byteLength
        : throw new ArgumentOutOfRangeException(nameof(byteLength));

    public override string GetDefaultValue()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(ByteLength));
    }

    public override void WriteToManifest(Aspire.Hosting.Publishing.ManifestPublishingContext context)
    {
        context.Writer.WriteStartObject("generateBase64");
        context.Writer.WriteNumber("byteLength", ByteLength);
        context.Writer.WriteEndObject();
    }
}
