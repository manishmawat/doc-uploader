using DocUploaderApi.Extensions;
using DocUploaderApi.Models;
using DocUploaderApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocUploaderApi.Controllers;

/// <summary>
/// API endpoints for browsing, downloading and deleting the signed-in user's documents
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = DocumentUploadController.UploadPolicy)]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IBlobStorageService blobStorageService,
        ILogger<DocumentsController> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Lists the documents in the signed-in user's folder, newest first
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<DocumentItem>>> List(CancellationToken cancellationToken)
    {
        var userIdentifier = User.GetUserIdentifier();
        if (string.IsNullOrEmpty(userIdentifier))
            return UserNotIdentified();

        try
        {
            return Ok(await _blobStorageService.ListUserDocumentsAsync(userIdentifier, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents for user {User}", userIdentifier);
            return Error(StatusCodes.Status500InternalServerError, "LIST_ERROR",
                "An error occurred while listing documents");
        }
    }

    /// <summary>
    /// Returns a read-only download URL for one of the user's documents (expires in 5 minutes)
    /// </summary>
    [HttpGet("{name}/download-url")]
    [ProducesResponseType(typeof(DownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrl(
        string name,
        CancellationToken cancellationToken)
    {
        var userIdentifier = User.GetUserIdentifier();
        if (string.IsNullOrEmpty(userIdentifier))
            return UserNotIdentified();

        try
        {
            var response = await _blobStorageService.GenerateDownloadSasAsync(userIdentifier, name, cancellationToken);
            return response is null ? NotFoundError(name) : Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Rejected download request for {Name}: {Error}", name, ex.Message);
            return Error(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_NAME", "Invalid document name");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download URL for {Name}", name);
            return Error(StatusCodes.Status500InternalServerError, "DOWNLOAD_ERROR",
                "An error occurred while preparing the download");
        }
    }

    /// <summary>
    /// Deletes one of the user's documents
    /// </summary>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(string name, CancellationToken cancellationToken)
    {
        var userIdentifier = User.GetUserIdentifier();
        if (string.IsNullOrEmpty(userIdentifier))
            return UserNotIdentified();

        try
        {
            var deleted = await _blobStorageService.DeleteUserDocumentAsync(userIdentifier, name, cancellationToken);
            return deleted ? NoContent() : NotFoundError(name);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Rejected delete request for {Name}: {Error}", name, ex.Message);
            return Error(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_NAME", "Invalid document name");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting {Name}", name);
            return Error(StatusCodes.Status500InternalServerError, "DELETE_ERROR",
                "An error occurred while deleting the document");
        }
    }

    private ObjectResult UserNotIdentified() =>
        Error(StatusCodes.Status401Unauthorized, "USER_NOT_IDENTIFIED",
            "Unable to identify user from authentication token");

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
