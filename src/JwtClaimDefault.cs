namespace Aspire.Hosting;

public sealed record JwtClaimDefault(
    string Value,
    bool UserConfigurable = false,
    string? Label = null,
    string? Description = null);