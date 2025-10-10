using System.Security.Claims;
using Adventures.Shared.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Adventures.Shared.Middleware;

public class UserPopulationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserPopulationMiddleware> _logger;

    public UserPopulationMiddleware(RequestDelegate next, ILogger<UserPopulationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // IUser is resolved per-request (scoped) and populated from claims
    public async Task InvokeAsync(HttpContext context, IUser user)
    {
        try
        {
            var principal = context.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                user.Id = GetFirst(principal, ClaimTypes.NameIdentifier, "sub");
                user.Name = GetFirst(principal, ClaimTypes.Name, "name", "preferred_username", "nickname");
                user.Email = GetFirst(principal, ClaimTypes.Email, "email");

                // Collect roles from common claim types (supports Auth0 custom namespaces too)
                var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var claim in principal.Claims)
                {
                    if (IsRoleClaim(claim.Type))
                    {
                        // ASP.NET maps JWT array claims to multiple claims of the same type.
                        // Still split to be safe if values are comma/space separated.
                        foreach (var part in SplitClaimValues(claim.Value))
                        {
                            if (!string.IsNullOrWhiteSpace(part)) roles.Add(part);
                        }
                    }
                }

                user.Roles = roles.Count > 0 ? roles.ToList() : user.Roles;
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Unauthenticated request
                user.IsActive = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to populate IUser from claims");
            // Do not block request flow on population failure
        }

        await _next(context);
    }

    private static string? GetFirst(ClaimsPrincipal principal, params string[] types)
    {
        foreach (var t in types)
        {
            var val = principal.FindFirstValue(t);
            if (!string.IsNullOrWhiteSpace(val)) return val;
        }
        return null;
    }

    private static bool IsRoleClaim(string claimType)
    {
        // Standard + common custom namespace endings
        if (string.Equals(claimType, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(claimType, "role", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(claimType, "roles", StringComparison.OrdinalIgnoreCase)) return true;
        // Handle Auth0 or other providers adding namespaced roles, e.g., https://your-app.com/roles
        if (claimType.EndsWith("/roles", StringComparison.OrdinalIgnoreCase)) return true;
        if (claimType.EndsWith("/role", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static IEnumerable<string> SplitClaimValues(string value)
    {
        // Split on common separators but also return the original if no separators
        return value
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
