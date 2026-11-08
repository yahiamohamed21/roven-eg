using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Stockify.Common.Constants;
using Stockify.Models.Entities;

namespace Stockify.Services.Identity;

public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Add FullName claim
        if (!string.IsNullOrEmpty(user.FullName))
        {
            identity.AddClaim(new Claim("FullName", user.FullName));
        }

        // Add Permission claims
        if (!string.IsNullOrWhiteSpace(user.Permissions))
        {
            var parts = user.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var perm in parts)
            {
                identity.AddClaim(new Claim("Permission", perm));
            }
        }
        else
        {
            // Fallback to role defaults
            var isAdmin = await UserManager.IsInRoleAsync(user, Roles.Admin);
            var defaultPerms = isAdmin ? Permissions.DefaultAdmin : Permissions.DefaultSales;
            foreach (var perm in defaultPerms)
            {
                identity.AddClaim(new Claim("Permission", perm));
            }
        }

        return identity;
    }
}

