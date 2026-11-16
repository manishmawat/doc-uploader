using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using DocUploaderApi.Models;
using BlobRequestConditions = Azure.Storage.Blobs.Models.BlobRequestConditions;
using BlobUploadOptions = Azure.Storage.Blobs.Models.BlobUploadOptions;
using BlobHttpHeaders = Azure.Storage.Blobs.Models.BlobHttpHeaders;

namespace DocUploaderApi.Services;

public interface ICardStore
{
    Task<IReadOnlyList<BarcodeCard>> ListAsync(string userIdentifier, CancellationToken cancellationToken = default);

    /// <summary>Adds a card; returns null when the user already has the maximum number of cards</summary>
    Task<BarcodeCard?> AddAsync(string userIdentifier, BarcodeCardRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns null when no card has that id</summary>
    Task<BarcodeCard?> UpdateAsync(string userIdentifier, string id, BarcodeCardRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a card and its photo</summary>
    Task<bool> DeleteAsync(string userIdentifier, string id, CancellationToken cancellationToken = default);

    /// <summary>Stores (or replaces) the card's photo; returns the updated card, or null when no card has that id</summary>
    Task<BarcodeCard?> SetPhotoAsync(string userIdentifier, string id, Stream photo, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Returns the photo, or null when the card or its photo doesn't exist</summary>
    Task<(Stream Content, string ContentType)?> GetPhotoAsync(string userIdentifier, string id, CancellationToken cancellationToken = default);

    /// <summary>Removes the card's photo; returns the updated card, or null when no card has that id</summary>
    Task<BarcodeCard?> RemovePhotoAsync(string userIdentifier, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps each user's cards as one JSON blob at .cards/&lt;user&gt;/cards.json (photos in
/// .cards/&lt;user&gt;/photos/&lt;card id&gt;), next to (not inside)
/// their documents folder, so My Documents never lists it. Writes use the blob's ETag, so two
/// devices saving at once can't overwrite each other's change; a conflicting write is retried
/// on the latest copy.
/// </summary>
public class BlobCardStore(BlobContainerClient containerClient, ILogger<BlobCardStore> logger) : ICardStore
{
    private const string Prefix = ".cards/";
    private const int MaxAttempts = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<BarcodeCard>> ListAsync(string userIdentifier, CancellationToken cancellationToken = default)
    {
        var (cards, _) = await ReadAsync(Blob(userIdentifier), cancellationToken);
        return cards.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public Task<BarcodeCard?> AddAsync(string userIdentifier, BarcodeCardRequest request, CancellationToken cancellationToken = default) =>
        ModifyAsync(userIdentifier, cards =>
        {
            if (cards.Count >= CardRules.MaxCards)
                return null;

            var now = DateTimeOffset.UtcNow;
            var card = new BarcodeCard { Id = Guid.NewGuid().ToString("N"), CreatedOn = now };
            Apply(card, request, now);
            cards.Add(card);
            return card;
        }, cancellationToken);

    public Task<BarcodeCard?> UpdateAsync(string userIdentifier, string id, BarcodeCardRequest request, CancellationToken cancellationToken = default) =>
        ModifyAsync(userIdentifier, cards =>
        {
            var card = cards.FirstOrDefault(c => c.Id == id);
            if (card is not null)
                Apply(card, request, DateTimeOffset.UtcNow);
            return card;
        }, cancellationToken);

    public async Task<bool> DeleteAsync(string userIdentifier, string id, CancellationToken cancellationToken = default)
    {
        if (await ModifyAsync(userIdentifier, cards => cards.RemoveAll(c => c.Id == id) > 0 ? "deleted" : null, cancellationToken) is null)
            return false;
        if (IsValidId(id))
            await PhotoBlob(userIdentifier, id).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return true;
    }

    public async Task<BarcodeCard?> SetPhotoAsync(string userIdentifier, string id, Stream photo, string contentType, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id) || (await ListAsync(userIdentifier, cancellationToken)).All(c => c.Id != id))
            return null;

        await PhotoBlob(userIdentifier, id).UploadAsync(photo, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);

        var card = await ModifyAsync(userIdentifier, cards =>
        {
            var match = cards.FirstOrDefault(c => c.Id == id);
            if (match is not null)
                match.PhotoUpdatedOn = DateTimeOffset.UtcNow;
            return match;
        }, cancellationToken);

        // The card was deleted while the photo uploaded: don't leave the photo behind
        if (card is null)
            await PhotoBlob(userIdentifier, id).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return card;
    }

    public async Task<(Stream Content, string ContentType)?> GetPhotoAsync(string userIdentifier, string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id))
            return null;
        try
        {
            var download = await PhotoBlob(userIdentifier, id).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return (download.Value.Content, download.Value.Details.ContentType ?? "image/jpeg");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<BarcodeCard?> RemovePhotoAsync(string userIdentifier, string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id))
            return null;

        var card = await ModifyAsync(userIdentifier, cards =>
        {
            var match = cards.FirstOrDefault(c => c.Id == id);
            if (match is not null)
                match.PhotoUpdatedOn = null;
            return match;
        }, cancellationToken);

        if (card is not null)
            await PhotoBlob(userIdentifier, id).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return card;
    }

    /// <summary>Card ids are 32 lowercase hex characters (see AddAsync); anything else can't name a photo blob</summary>
    private static bool IsValidId(string id) =>
        id.Length == 32 && id.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void Apply(BarcodeCard card, BarcodeCardRequest request, DateTimeOffset now)
    {
        card.Name = request.Name.Trim();
        card.Format = request.Format;
        card.Value = request.Value;
        card.Color = request.Color ?? card.Color;
        card.UpdatedOn = now;
    }

    /// <summary>
    /// Reads the user's cards, applies <paramref name="change"/> and saves them if it returned a
    /// result. Saving is conditional on the ETag that was read; on a conflict it starts again.
    /// </summary>
    private async Task<T?> ModifyAsync<T>(string userIdentifier, Func<List<BarcodeCard>, T?> change, CancellationToken cancellationToken)
        where T : class
    {
        var blob = Blob(userIdentifier);
        for (var attempt = 1; ; attempt++)
        {
            var (cards, etag) = await ReadAsync(blob, cancellationToken);
            var result = change(cards);
            if (result is null)
                return null;

            var conditions = etag is { } e ? new BlobRequestConditions { IfMatch = e } : new BlobRequestConditions { IfNoneMatch = ETag.All };
            try
            {
                await blob.UploadAsync(BinaryData.FromObjectAsJson(cards, JsonOptions), new BlobUploadOptions
                {
                    Conditions = conditions,
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                }, cancellationToken);
                return result;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412 && attempt < MaxAttempts)
            {
                // Someone else saved first: back off a little (randomised, so writers don't collide again)
                logger.LogInformation("Cards for {User} changed while saving; retrying (attempt {Attempt})", userIdentifier, attempt);
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, cancellationToken);
            }
        }
    }

    private static async Task<(List<BarcodeCard> Cards, ETag? ETag)> ReadAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        try
        {
            var content = await blob.DownloadContentAsync(cancellationToken);
            var cards = content.Value.Content.ToObjectFromJson<List<BarcodeCard>>(JsonOptions) ?? [];
            return (cards, content.Value.Details.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return ([], null);
        }
    }

    private BlobClient Blob(string userIdentifier) =>
        containerClient.GetBlobClient($"{Prefix}{BlobStorageService.SanitizePath(userIdentifier)}/cards.json");

    private BlobClient PhotoBlob(string userIdentifier, string id) =>
        containerClient.GetBlobClient($"{Prefix}{BlobStorageService.SanitizePath(userIdentifier)}/photos/{id}");
}
