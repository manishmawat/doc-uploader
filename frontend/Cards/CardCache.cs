using System.Security.Claims;
using System.Text.Json;
using Microsoft.JSInterop;

namespace UploaderDoc.Cards;

/// <summary>
/// The copy of the user's cards kept in this browser, so they can be shown without a connection.
/// Stored per user (so a shared device doesn't mix accounts) and cleared on sign-out.
/// Storage can be blocked (private mode); then there's simply no offline copy.
/// </summary>
public class CardCache(IJSRuntime jsRuntime)
{
    public const string KeyPrefix = "uploaderdoc.cards.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<BarcodeCard>?> LoadAsync(ClaimsPrincipal user)
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", Key(user));
            return json is null ? null : JsonSerializer.Deserialize<List<BarcodeCard>>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(ClaimsPrincipal user, IEnumerable<BarcodeCard> cards)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", Key(user), JsonSerializer.Serialize(cards, JsonOptions));
        }
        catch (JSException)
        {
        }
    }

    private static string Key(ClaimsPrincipal user) => KeyPrefix + UserKey(user);

    /// <summary>Entra object id (stable per user), falling back to other identifiers.</summary>
    public static string UserKey(ClaimsPrincipal user) =>
        (user.FindFirst("oid")?.Value
            ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.Identity?.Name
            ?? "user");
}
