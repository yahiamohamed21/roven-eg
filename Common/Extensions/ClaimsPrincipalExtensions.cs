using System.Security.Claims;
using Stockify.Common.Constants;

namespace Stockify.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool HasPermission(this ClaimsPrincipal principal, string permissionKey)
    {
        if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            return false;

        // Super-admin master account always has full access
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.Equals(email, "admin@roven.local", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(email, "admin@stockify.local", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if the user has this permission claim
        if (principal.HasClaim("Permission", permissionKey))
            return true;

        return false;
    }

    public static string GetUserFullName(this ClaimsPrincipal principal)
    {
        return principal?.FindFirst("FullName")?.Value 
               ?? principal?.FindFirstValue(ClaimTypes.Name) 
               ?? "User";
    }
}

