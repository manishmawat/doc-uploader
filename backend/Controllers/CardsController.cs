using DocUploaderApi.Extensions;
using DocUploaderApi.Models;
using DocUploaderApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocUploaderApi.Controllers;

/// <summary>
/// The signed-in user's saved barcode cards (gym, library, loyalty cards)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = DocumentUploadController.UploadPolicy)]
[Produces("application/json")]
public class CardsController(ICardStore cardStore, ILogger<CardsController> logger) : ControllerBase
{
    /// <summary>
    /// Lists the user's cards, sorted by name
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BarcodeCard>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> List(CancellationToken cancellationToken) =>
        ForUser("LIST_ERROR", "An error occurred while loading your cards",
            async user => Ok(await cardStore.ListAsync(user, cancellationToken)));

    /// <summary>
    /// Saves a new card
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BarcodeCard), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Create([FromBody] BarcodeCardRequest request, CancellationToken cancellationToken) =>
        ForUser("SAVE_ERROR", "An error occurred while saving the card", async user =>
        {
            if (CardRules.Validate(request) is { } error)
                return Error(StatusCodes.Status400BadRequest, "INVALID_CARD", error);

            return await cardStore.AddAsync(user, request, cancellationToken) is { } card
                ? StatusCode(StatusCodes.Status201Created, card)
                : Error(StatusCodes.Status409Conflict, "TOO_MANY_CARDS", $"You can save up to {CardRules.MaxCards} cards");
        });

    /// <summary>
    /// Changes a card's name, barcode or colour
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(BarcodeCard), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(string id, [FromBody] BarcodeCardRequest request, CancellationToken cancellationToken) =>
        ForUser("SAVE_ERROR", "An error occurred while saving the card", async user =>
        {
            if (CardRules.Validate(request) is { } error)
                return Error(StatusCodes.Status400BadRequest, "INVALID_CARD", error);

            return await cardStore.UpdateAsync(user, id, request, cancellationToken) is { } card
                ? Ok(card)
                : CardNotFound();
        });

    /// <summary>
    /// Deletes a card
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ForUser("DELETE_ERROR", "An error occurred while deleting the card",
            async user => await cardStore.DeleteAsync(user, id, cancellationToken) ? NoContent() : CardNotFound());

    /// <summary>
    /// Sets the card's photo. Send the image as the request body (image/jpeg, image/png or image/webp, up to 2 MB).
    /// </summary>
    [HttpPut("{id}/photo")]
    [RequestSizeLimit(CardRules.MaxPhotoBytes + 64 * 1024)]
    [ProducesResponseType(typeof(BarcodeCard), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> SetPhoto(string id, CancellationToken cancellationToken) =>
        ForUser("PHOTO_ERROR", "An error occurred while saving the photo", async user =>
        {
            var contentType = Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
            if (contentType is null || !CardRules.PhotoContentTypes.Contains(contentType))
                return Error(StatusCodes.Status400BadRequest, "INVALID_PHOTO", "The photo must be a JPEG, PNG or WebP image");

            // Read into memory with a hard cap (the body may not declare its length)
            using var photo = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (photo.Length + read > CardRules.MaxPhotoBytes)
                    return Error(StatusCodes.Status400BadRequest, "PHOTO_TOO_LARGE", "The photo can be at most 2 MB");
                photo.Write(buffer, 0, read);
            }
            if (photo.Length == 0)
                return Error(StatusCodes.Status400BadRequest, "INVALID_PHOTO", "The photo is empty");

            photo.Position = 0;
            return await cardStore.SetPhotoAsync(user, id, photo, contentType, cancellationToken) is { } card
                ? Ok(card)
                : CardNotFound();
        });

    /// <summary>
    /// Returns the card's photo
    /// </summary>
    [HttpGet("{id}/photo")]
    [Produces("image/jpeg", "image/png", "image/webp", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetPhoto(string id, CancellationToken cancellationToken) =>
        ForUser("PHOTO_ERROR", "An error occurred while loading the photo", async user =>
            await cardStore.GetPhotoAsync(user, id, cancellationToken) is { } photo
                ? File(photo.Content, photo.ContentType)
                : Error(StatusCodes.Status404NotFound, "PHOTO_NOT_FOUND", "This card has no photo"));

    /// <summary>
    /// Removes the card's photo
    /// </summary>
    [HttpDelete("{id}/photo")]
    [ProducesResponseType(typeof(BarcodeCard), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> RemovePhoto(string id, CancellationToken cancellationToken) =>
        ForUser("PHOTO_ERROR", "An error occurred while removing the photo", async user =>
            await cardStore.RemovePhotoAsync(user, id, cancellationToken) is { } card ? Ok(card) : CardNotFound());

    private async Task<IActionResult> ForUser(string errorCode, string errorMessage, Func<string, Task<IActionResult>> action)
    {
        var userIdentifier = User.GetUserIdentifier();
        if (string.IsNullOrEmpty(userIdentifier))
            return Error(StatusCodes.Status401Unauthorized, "USER_NOT_IDENTIFIED",
                "Unable to identify user from authentication token");

        try
        {
            return await action(userIdentifier);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Action} failed for user {User}", ControllerContext.ActionDescriptor.ActionName, userIdentifier);
            return Error(StatusCodes.Status500InternalServerError, errorCode, errorMessage);
        }
    }

    private ObjectResult CardNotFound() =>
        Error(StatusCodes.Status404NotFound, "CARD_NOT_FOUND", "Card not found");

    private ObjectResult Error(int statusCode, string code, string message) =>
        StatusCode(statusCode, new ErrorResponse
        {
            Code = code,
            Message = message,
            RequestId = HttpContext.TraceIdentifier
        });
}
