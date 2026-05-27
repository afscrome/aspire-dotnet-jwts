using System.Security.Claims;
using aspirefriday.ApiService;
using Microsoft.AspNetCore.Authorization;

namespace aspirefriday.UnitTests;

public class MinimumAgeAuthorizationHandlerTests
{
    [Test]
    public async Task HandleRequirementAsync_Succeeds_WhenAgeMeetsMinimum()
    {
        var requirement = new MinimumAgeRequirement(21);
        var context = CreateContext(requirement, age: "21");

        var handler = new MinimumAgeAuthorizationHandler();

        await handler.HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsTrue();
    }

    [Test]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenAgeIsBelowMinimum()
    {
        var requirement = new MinimumAgeRequirement(21);
        var context = CreateContext(requirement, age: "20");

        var handler = new MinimumAgeAuthorizationHandler();

        await handler.HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsFalse();
    }

    [Test]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenAgeClaimIsMissing()
    {
        var requirement = new MinimumAgeRequirement(18);
        var context = CreateContext(requirement, age: null);

        var handler = new MinimumAgeAuthorizationHandler();

        await handler.HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsFalse();
    }

    private static AuthorizationHandlerContext CreateContext(MinimumAgeRequirement requirement, string? age)
    {
        var claims = new List<Claim>();

        if (age is not null)
        {
            claims.Add(new Claim("age", age));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var user = new ClaimsPrincipal(identity);

        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }
}