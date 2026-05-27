var builder = DistributedApplication.CreateBuilder(args);

var signingKey = builder.AddJwtSigningToken("signing-key");

builder.AddProject<Projects.aspirefriday_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithJwtToken(
        signingKey,
        commandName: "jwt-user",
        displayName: "Generate Token",
        description: "Generate a signed user JWT bearer token using the configured static signing key.",
        additionalClaims: new Dictionary<string, JwtClaimDefault>
        {
            ["aud"] = new("dotnetappwithauth"),
            ["sub"] = new("dev-user", UserConfigurable: true, Label: "User ID", Description: "Value used for the sub claim."),
            ["age"] = new("18", UserConfigurable: true, Label: "Age claim", Description: "User's age used for authorization checks."),
        })
    .WithJwtToken(
        signingKey,
        commandName: "jwt-user-under18",
        displayName: "Generate Under 18 Token",
        description: "Generate a signed user JWT bearer token using the configured static signing key.",
        additionalClaims: new Dictionary<string, JwtClaimDefault>
        {
            ["aud"] = new("dotnetappwithauth"),
            ["sub"] = new("dev-user"),
            ["age"] = new("16"),
        });


builder.Build().Run();

