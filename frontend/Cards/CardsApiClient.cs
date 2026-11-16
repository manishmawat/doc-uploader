using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using UploaderDoc.Services;

namespace UploaderDoc.Cards;

/// <summary>A saved barcode card (matches the API's BarcodeCard).</summary>
public class BarcodeCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Color { get; set; } = "#0f6cbd";
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }

    /// <summary>When the photo was last set; null when the card has no photo</summary>
    public DateTimeOffset? PhotoUpdatedOn { get; set; }
}

public record BarcodeCardRequest(string Name, string Format, string Value, string Color);

/// <summary>
/// Thrown when the cards API can't be used. <see cref="IsOffline"/> means the request never
/// reached the API (no connection), so the page can fall back to the copy on the device.
/// </summary>
public class CardsApiException(string message, bool isOffline = false) : Exception(message)
{
    public bool IsOffline { get; } = isOffline;
}

/// <summary>Calls /api/Cards with the signed-in user's token.</summary>
public class CardsApiClient(
    HttpClient httpClient,
    IAccessTokenProvider accessTokenProvider,
    IConfiguration configuration,
    IOptions<BlobStorageOptions> storageOptions,
    IJSRuntime jsRuntime)
{
    private string BaseUrl => $"{configuration["Api:BaseUrl"]?.TrimEnd('/')}/api/Cards";

    public async Task<List<BarcodeCard>> ListAsync() =>
        await SendAsync<List<BarcodeCard>>(HttpMethod.Get, BaseUrl) ?? [];

    public async Task<BarcodeCard> CreateAsync(BarcodeCardRequest request) =>
        (await SendAsync<BarcodeCard>(HttpMethod.Post, BaseUrl, request))!;

    public async Task<BarcodeCard> UpdateAsync(string id, BarcodeCardRequest request) =>
        (await SendAsync<BarcodeCard>(HttpMethod.Put, $"{BaseUrl}/{Uri.EscapeDataString(id)}", request))!;

    public Task DeleteAsync(string id) =>
        SendAsync<object>(HttpMethod.Delete, $"{BaseUrl}/{Uri.EscapeDataString(id)}");

    /// <summary>Sets the card's photo (a JPEG); returns the updated card</summary>
    public async Task<BarcodeCard> SetPhotoAsync(string id, byte[] jpeg)
    {
        var content = new ByteArrayContent(jpeg);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        using var response = await SendRawAsync(HttpMethod.Put, PhotoUrl(id), content);
        return (await response.Content.ReadFromJsonAsync<BarcodeCard>())!;
    }

    public async Task<BarcodeCard> RemovePhotoAsync(string id) =>
        (await SendAsync<BarcodeCard>(HttpMethod.Delete, PhotoUrl(id)))!;

    public async Task<byte[]> GetPhotoAsync(string id)
    {
        using var response = await SendRawAsync(HttpMethod.Get, PhotoUrl(id), null);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private string PhotoUrl(string id) => $"{BaseUrl}/{Uri.EscapeDataString(id)}/photo";

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendRawAsync(method, url, body is null ? null : JsonContent.Create(body));
        return response.StatusCode == System.Net.HttpStatusCode.NoContent
            ? default
            : await response.Content.ReadFromJsonAsync<T>();
    }

    /// <summary>Sends the request with the user's token; throws <see cref="CardsApiException"/> unless it succeeds</summary>
    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, HttpContent? content)
    {
        var tokenResult = await accessTokenProvider.RequestAccessToken(new AccessTokenRequestOptions
        {
            Scopes = storageOptions.Value.Scopes
        });
        // Don't redirect to sign-in from here: offline, that would leave the page with nothing to show
        if (!tokenResult.TryGetToken(out var token))
        {
            // Offline, an expired token can't be refreshed: that's "offline", not a sign-in problem
            if (!await IsOnlineAsync())
                throw new CardsApiException("You're offline.", isOffline: true);
            throw new CardsApiException("Your sign-in has expired. Sign out and in again to sync your cards.");
        }

        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new CardsApiException("You're offline.", isOffline: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>().ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : null);
                throw new CardsApiException(error?.Message ?? $"The server returned {(int)response.StatusCode}.");
            }
        }
        return response;
    }

    private async Task<bool> IsOnlineAsync()
    {
        try
        {
            await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/cards.js");
            return await module.InvokeAsync<bool>("isOnline");
        }
        catch (JSException)
        {
            return true;
        }
    }

    private record ApiError(string? Message);
}
