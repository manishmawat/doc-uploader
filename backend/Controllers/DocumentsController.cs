using DocUploaderApi.Extensions;
using DocUploaderApi.Models;
using DocUploaderApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocUploaderApi.Controllers;

/// <summary>
/// API endpoints for the signed-in user's documents: browse, download, share,
/// and a recycle bin for deleted documents
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = DocumentUploadController.UploadPolicy)]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly IOptions<BlobUploadOptions> _uploadOptions;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IBlobStorageService blobStorageService,
        IOptions<BlobUploadOptions> uploadOptions,
        ILogger<DocumentsController> logger)
    {
        _blobStorageService = blobStorageService;
        _uploadOptions = uploadOptions;
        _logger = logger;
    }

    /// <summary>
    /// Lists the documents in the signed-in user's folder, newest first
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> List(CancellationToken cancellationToken) =>
        ForUser(null, "LIST_ERROR", "An error occurred while listing documents",
            async user => Ok(await _blobStorageService.ListUserDocumentsAsync(user, cancellationToken)));

    /// <summary>
    /// Returns a read-only download URL for one of the user's documents (expires in 5 minutes)
    /// </summary>
    [HttpGet("{name}/download-url")]
    [ProducesResponseType(typeof(DownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetDownloadUrl(string name, CancellationToken cancellationToken) =>
        ForUser(name, "DOWNLOAD_ERROR", "An error occurred while preparing the download",
            async user => await _blobStorageService.GenerateDownloadSasAsync(user, name, cancellationToken) is { } response
                ? Ok(response)
                : NotFoundError(name));

    /// <summary>
    /// Creates a read-only link anyone can open until it expires
    /// </summary>
    /// <remarks>
    /// The link can't be revoked; moving the document to the recycle bin makes it stop working.
    /// `expiresInHours` must be one of `BlobUpload:ShareLinkAllowedHours` (default 1, 24, 72).
    /// </remarks>
    [HttpPost("{name}/share")]
    [ProducesResponseType(typeof(DownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> Share(string name, [FromBody] ShareLinkRequest request, CancellationToken cancellationToken)
    {
        var allowedHours = _uploadOptions.Value.ShareLinkAllowedHours;
        if (!allowedHours.Contains(request.ExpiresInHours))
        {
            return Task.FromResult<IActionResult>(Error(StatusCodes.Status400BadRequest, "INVALID_EXPIRY",
                $"expiresInHours must be one of: {string.Join(", ", allowedHours)}"));
        }

        return ForUser(name, "SHARE_ERROR", "An error occurred while creating the share link",
            async user => await _blobStorageService.CreateShareLinkAsync(user, name, request.ExpiresInHours, cancellationToken) is { } response
                ? Ok(response)
                : NotFoundError(name));
    }

    /// <summary>
    /// Moves one of the user's documents to their recycle bin
    /// </summary>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> Delete(string name, CancellationToken cancellationToken) =>
        ForUser(name, "DELETE_ERROR", "An error occurred while deleting the document",
            async user => await _blobStorageService.MoveToRecycleBinAsync(user, name, cancellationToken)
                ? NoContent()
                : NotFoundError(name));

    /// <summary>
    /// Lists the user's recycle bin, most recently deleted first
    /// </summary>
    [HttpGet("recycle-bin")]
    [ProducesResponseType(typeof(IReadOnlyList<RecycleBinItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> ListRecycleBin(CancellationToken cancellationToken) =>
        ForUser(null, "LIST_ERROR", "An error occurred while listing the recycle bin",
            async user => Ok(await _blobStorageService.ListRecycleBinAsync(user, cancellationToken)));

    /// <summary>
    /// Moves a document from the recycle bin back to the user's documents
    /// </summary>
    [HttpPost("recycle-bin/{name}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> Restore(string name, CancellationToken cancellationToken) =>
        ForUser(name, "RESTORE_ERROR", "An error occurred while restoring the document",
            async user => await _blobStorageService.RestoreFromRecycleBinAsync(user, name, cancellationToken) switch
            {
                RestoreResult.Restored => NoContent(),
                RestoreResult.NameConflict => Error(StatusCodes.Status409Conflict, "NAME_CONFLICT",
                    $"A document named '{name}' already exists. Rename or delete it, then restore again."),
                _ => NotFoundError(name)
            });

    /// <summary>
    /// Permanently deletes a document from the recycle bin
    /// </summary>
    [HttpDelete("recycle-bin/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> DeleteForever(string name, CancellationToken cancellationToken) =>
        ForUser(name, "DELETE_ERROR", "An error occurred while deleting the document",
            async user => await _blobStorageService.DeleteFromRecycleBinAsync(user, name, cancellationToken)
                ? NoContent()
                : NotFoundError(name));

    /// <summary>
    /// Resolves the signed-in user and maps invalid names (ArgumentException) to 400
    /// and unexpected failures to 500
    /// </summary>
    private async Task<IActionResult> ForUser(
        string? name,
        string errorCode,
        string errorMessage,
        Func<string, Task<IActionResult>> action)
    {
        var userIdentifier = User.GetUserIdentifier();
        if (string.IsNullOrEmpty(userIdentifier))
            return Error(StatusCodes.Status401Unauthorized, "USER_NOT_IDENTIFIED",
                "Unable to identify user from authentication token");

        try
        {
            return await action(userIdentifier);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Rejected {Action} request for {Name}: {Error}",
                ControllerContext.ActionDescriptor.ActionName, name, ex.Message);
            return Error(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_NAME", "Invalid document name");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Action} failed for user {User}, document {Name}",
                ControllerContext.ActionDescriptor.ActionName, userIdentifier, name);
            return Error(StatusCodes.Status500InternalServerError, errorCode, errorMessage);
        }
    }

    private ObjectResult NotFoundError(string name) =>
        Error(StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND", $"Document '{name}' was not found");

    private ObjectResult Error(int statusCode, string code, string message) =>
        StatusCode(statusCode, new ErrorResponse
        {
            Code = code,
            Message = message,
            RequestId = HttpContext.TraceIdentifier
        });
}
