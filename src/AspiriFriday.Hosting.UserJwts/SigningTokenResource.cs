using Aspire.Hosting.ApplicationModel;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Aspire.Hosting;

public sealed class SigningTokenResource(
    [ResourceName]string name,
    ParameterResource signingKeyParameter,
    string algorithm) : Resource(name)
{
    public string Issuer { get; set; } = $"Test-Jwt-{name}";
    public ParameterResource SigningKeyParameter { get; } = signingKeyParameter;
    public string KeyId { get; } = name;
    public string Algorithm { get; } = algorithm;
    public Dictionary<string, object> DefaultClaims { get; } = new(StringComparer.Ordinal);
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromHours(1);

    public async Task<string> GenerateJwtAsync(
        IDictionary<string, object> claims,
        CancellationToken cancellationToken = default)
    {
        var signingKeyBase64 = await SigningKeyParameter.GetValueAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(signingKeyBase64))
        {
            throw new InvalidOperationException("Signing key parameter value is not set.");
        }

        var keyBytes = Convert.FromBase64String(signingKeyBase64);
        var securityKey = new SymmetricSecurityKey(keyBytes) { KeyId = KeyId };
        var credentials = new SigningCredentials(securityKey, Algorithm);

        var now = DateTime.UtcNow;
        
        var allClaims = new Dictionary<string, object>(DefaultClaims, StringComparer.Ordinal);
        foreach (var claim in claims)
        {
            allClaims[claim.Key] = claim.Value;
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            NotBefore = now,
            Expires = now.Add(DefaultLifetime),
            SigningCredentials = credentials,
            Claims = allClaims,
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}