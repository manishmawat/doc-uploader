using System.Security.Claims;
using Microsoft.JSInterop;

namespace UploaderDoc.Cards;

/// <summary>What the card editor saves: the card fields, plus a new photo or a request to remove the photo.</summary>
public record CardEditResult(BarcodeCardRequest Card, byte[]? NewPhoto, bool RemovePhoto);

/// <summary>
/// Card photos for display: from the copy on this device when it's current, otherwise downloaded
/// from the API and kept on the device (Cache Storage, via wwwroot/js/cards.js) for offline use.
/// Returns browser object URLs; call <see cref="RevokeAsync"/> when they're no longer shown.
/// </summary>
public sealed class CardPhotos(IJSRuntime jsRuntime, CardsApiClient api) : IAsyncDisposable
{
    private IJSObjectReference? module;

    /// <summary>The card's photo, or null when it has none or it can't be loaded (e.g. offline and not on the device).</summary>
    public async Task<string?> GetUrlAsync(ClaimsPrincipal user, BarcodeCard card)
    {
        if (card.PhotoUpdatedOn is null)
            return null;

        var js = await ModuleAsync();
        var key = CacheKey(user, card);
        if (await js.InvokeAsync<string?>("cachedPhotoUrl", key) is { } cached)
            return cached;

        try
        {
            var bytes = await api.GetPhotoAsync(card.Id);
            return await js.InvokeAsync<string>("storePhoto", key, bytes);
        }
        catch (CardsApiException)
        {
            return null;
        }
    }

    /// <summary>Keeps a just-uploaded photo on the device and returns a URL for it.</summary>
    public async Task<string> StoreAsync(ClaimsPrincipal user, BarcodeCard card, byte[] jpeg) =>
        await (await ModuleAsync()).InvokeAsync<string>("storePhoto", CacheKey(user, card), jpeg);

    public async Task ForgetAsync(ClaimsPrincipal user, string cardId) =>
        await (await ModuleAsync()).InvokeVoidAsync("forgetPhoto", CardPrefix(user, cardId));

    /// <summary>A URL for a photo that isn't saved yet (editor preview).</summary>
    public async Task<string> PreviewUrlAsync(byte[] jpeg) =>
        await (await ModuleAsync()).InvokeAsync<string>("photoUrl", jpeg);

    public async Task RevokeAsync(string? url)
    {
        if (url is not null)
            await (await ModuleAsync()).InvokeVoidAsync("revokeUrl", url);
    }

    /// <summary>One key per photo version, so a changed photo is never served from an old copy.</summary>
    private static string CacheKey(ClaimsPrincipal user, BarcodeCard card) =>
        $"{CardPrefix(user, card.Id)}{card.PhotoUpdatedOn!.Value.ToUnixTimeMilliseconds()}";

    private static string CardPrefix(ClaimsPrincipal user, string cardId) =>
        $"/card-photos/{Uri.EscapeDataString(CardCache.UserKey(user))}/{Uri.EscapeDataString(cardId)}/";

    private async Task<IJSObjectReference> ModuleAsync() =>
        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/cards.js");

    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;
        try
        {
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
