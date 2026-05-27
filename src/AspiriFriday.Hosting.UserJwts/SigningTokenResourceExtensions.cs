using Aspire.Hosting.ApplicationModel;
using Microsoft.IdentityModel.Tokens;

#pragma warning disable ASPIREINTERACTION001

namespace Aspire.Hosting;

public static class SigningTokenResourceExtensions
{
    public static IResourceBuilder<SigningTokenResource> AddJwtSigningToken(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        var jwtSigningKey = builder.AddParameter(
            "jwt-signing-key",
            new Base64ParameterDefault(32),
            secret: true,
            persist: true);

        var resource = new SigningTokenResource(
            name,
            jwtSigningKey.Resource,
            SecurityAlgorithms.HmacSha256);

        var state = new CustomResourceSnapshot
        {
            ResourceType = "SigningToken",
            CreationTimeStamp = DateTime.UtcNow,
            Properties =
            [
                new("issuer", resource.Issuer),
                new("keyId", resource.KeyId),
                new("algorithm", resource.Algorithm)
            ],
        };

        return builder.AddResource(resource)
            .ExcludeFromManifest()
            .WithIconName("Certificate")
            .WithInitialState(state)
            .OnInitializeResource(async (res, evt, ct) =>
            {
                await evt.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, evt.Services), ct);

                await evt.Notifications.PublishUpdateAsync(resource, s => s with
                {
                    StartTimeStamp = DateTime.UtcNow,
                    State = KnownResourceStates.Active,
                    Properties = [
                        ..s.Properties,
                        ..res.DefaultClaims.Select(x => new ResourcePropertySnapshot(x.Key, x.Value))
                    ]
                });
            });
    }

    public static IResourceBuilder<SigningTokenResource> WithIssuer(
        this IResourceBuilder<SigningTokenResource> builder,
        string issuer)
    {
        builder.Resource.Issuer = issuer;
        return builder;
    }

    public static IResourceBuilder<SigningTokenResource> WithDefaultLifetime(
        this IResourceBuilder<SigningTokenResource> builder,
        TimeSpan lifetime)
    {
        builder.Resource.DefaultLifetime = lifetime;
        return builder;
    }

    public static IResourceBuilder<SigningTokenResource> WithDefaultClaim(
        this IResourceBuilder<SigningTokenResource> builder,
        string claimType,
        object value)
    {
        builder.Resource.DefaultClaims[claimType] = value;
        return builder;
    }
}

#pragma warning restore ASPIREINTERACTION001
