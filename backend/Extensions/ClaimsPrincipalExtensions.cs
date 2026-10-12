using System.Security.Claims;

namespace DocUploaderApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts user identifier from JWT claims
    /// Tries multiple common claim types used by Azure AD/Entra
    /// </summary>
    public static string? GetUserIdentifier(this ClaimsPrincipal user)
    {
        // Try common claim types in order of preference
        var claimTypes = new[]
        {
            "email",               // Entra ID optional claim (real address for guests)
            ClaimTypes.Email,      // Standard email claim (mapped)
            "preferred_username",  // Entra ID UPN (user_domain#EXT#@tenant for guests)
            "emails",              // Some configurations
            ClaimTypes.NameIdentifier, // User ID as fallback
        };

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }
}
