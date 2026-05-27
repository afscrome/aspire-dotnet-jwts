using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace aspirefriday.UnitTests;

public class SigningTokenResourceTests
{
    private const string TestResourceName = "test-signing-token";
    private const string TestAlgorithm = SecurityAlgorithms.HmacSha256;

    private Dictionary<string,object> EmptyClaims = [];

    [Test]
    public async Task GenerateJwtAsync_CreatesValidToken_WithProvidedClaims()
    {
        var resource = CreateSigningKeyResource();

        var claims = new Dictionary<string, object>
        {
            { "sub", "user123" },
            { "email", "test@example.com" },
            { "role", "admin" }
        };

        var token = await resource.GenerateJwtAsync(claims);

        await Assert.That(token).IsNotNull().And.IsNotEmpty();
        
        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "sub" && c.Value == "user123");
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "email" && c.Value == "test@example.com");
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "role" && c.Value == "admin");
    }

    [Test]
    public async Task GenerateJwtAsync_IncludesDefaultClaims_WhenSet()
    {
        var resource = CreateSigningKeyResource();
        
        // Set default claims on the resource
        resource.DefaultClaims["default-claim"] = "default-value";
        resource.DefaultClaims["tenant-id"] = "tenant-123";

        var claims = new Dictionary<string, object>
        {
            { "sub", "user123" },
            { "custom-claim", "custom-value" }
        };

        var token = await resource.GenerateJwtAsync(claims);

        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "default-claim" && c.Value == "default-value");
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "tenant-id" && c.Value == "tenant-123");
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "sub" && c.Value == "user123");
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "custom-claim" && c.Value == "custom-value");
    }

    [Test]
    public async Task GenerateJwtAsync_ProvidedClaimsOverrideDefaultClaims()
    {
        var resource = CreateSigningKeyResource();
        
        resource.DefaultClaims["sub"] = "default-user";

        var claims = new Dictionary<string, object>
        {
            { "sub", "override-user" }
        };

        var token = await resource.GenerateJwtAsync(claims);

        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "sub" && c.Value == "override-user");
        await Assert.That(jwtToken.Claims).DoesNotContain(c => c.Type == "sub" && c.Value == "default-user");
    }

    [Test]
    public async Task GenerateJwtAsync_IncludesCorrectIssuer()
    {
        var resource = CreateSigningKeyResource();
        resource.Issuer = "test-issuer";

        var token = await resource.GenerateJwtAsync(EmptyClaims);

        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Issuer).IsEqualTo("test-issuer");
    }

    [Test]
    public async Task GenerateJwtAsync_IncludesKeyId()
    {
        var resource = CreateSigningKeyResource();

        var token = await resource.GenerateJwtAsync(EmptyClaims);

        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Kid).IsEqualTo(TestResourceName);
    }

    [Test]
    public async Task GenerateJwtAsync_ThrowsException_WhenSigningKeyParameterIsEmpty()
    {
        var resource = CreateSigningKeyResource(signingKey: string.Empty);

        await Assert.That(async () => await resource.GenerateJwtAsync(EmptyClaims))
            .Throws<InvalidOperationException>()
            .WithMessage("Signing key parameter value is not set.");
    }

    [Test]
    public async Task GenerateJwtAsync_ThrowsException_WhenSigningKeyParameterIsWhitespace()
    {
        var resource = CreateSigningKeyResource(signingKey: "   ");

        await Assert.That(async () => await resource.GenerateJwtAsync(EmptyClaims))
            .Throws<InvalidOperationException>()
            .WithMessage("Signing key parameter value is not set.");
    }

    [Test]
    public async Task GenerateJwtAsync_IncludesExpirationClaim()
    {
        var expectedDuration = TimeSpan.FromMinutes(37);
        var resource = CreateSigningKeyResource();
        resource.DefaultLifetime = expectedDuration;

        var beforeGeneration = DateTime.UtcNow;
        var validToIsh = beforeGeneration.Add(expectedDuration);
        var token = await resource.GenerateJwtAsync(EmptyClaims);

        var jwtToken = ReadJwtToken(token);

        var actualDuration = jwtToken.ValidTo - jwtToken.ValidFrom;
        await Assert.That(actualDuration).IsEqualTo(expectedDuration);
        await Assert.That(jwtToken.ValidFrom).IsEqualTo(beforeGeneration).Within(TimeSpan.FromSeconds(2));
        await Assert.That(jwtToken.ValidTo).IsEqualTo(validToIsh).Within(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task GenerateJwtAsync_HandlesEmptyClaimsCollection()
    {
        var resource = CreateSigningKeyResource();

        var token = await resource.GenerateJwtAsync(EmptyClaims);

        await Assert.That(token).IsNotNull().And.IsNotEmpty();
        
        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Issuer).IsEqualTo(resource.Issuer);
        await Assert.That(jwtToken.ValidFrom).IsNotEqualTo(DateTime.MinValue);
        await Assert.That(jwtToken.ValidTo).IsNotEqualTo(DateTime.MinValue);
    }

    [Test]
    public async Task GenerateJwtAsync_RespectsCancellationToken()
    {
        var resource = CreateSigningKeyResource();

        var cancellationTokenSource = new CancellationTokenSource();

        var token = await resource.GenerateJwtAsync(EmptyClaims, cancellationTokenSource.Token);

        await Assert.That(token).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task GenerateJwtAsync_IncludesAudienceClaim_WhenConfigured()
    {
        var resource = CreateSigningKeyResource();
        resource.DefaultClaims["aud"] = "aspirefriday-api";

        var claims = new Dictionary<string, object>
        {
            { "sub", "user123" },
            {"aud", "overridden-audience" }
        };

        var token = await resource.GenerateJwtAsync(claims);

        var jwtToken = ReadJwtToken(token);
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "aud" && c.Value == "overridden-audience");
        await Assert.That(jwtToken.Claims).Contains(c => c.Type == "sub" && c.Value == "user123");
    }


    private static SigningTokenResource CreateSigningKeyResource(string? signingKey = null)
    {
        signingKey ??= Convert.ToBase64String(GenerateRandomKey(32));
        var parameterResource = new ParameterResource("foo", x => signingKey!);

        return new SigningTokenResource(
            TestResourceName,
            parameterResource,
            TestAlgorithm);
    }

    private static JsonWebToken ReadJwtToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        return handler.ReadToken(token) as JsonWebToken ?? throw new InvalidOperationException("Invalid token");
    }

    private static byte[] GenerateRandomKey(int length)
    {
        var key = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return key;
    }

}
