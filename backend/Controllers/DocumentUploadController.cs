using DocUploaderApi.Models;
using DocUploaderApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocUploaderApi.Controllers;

/// <summary>
/// API endpoints for managing document uploads to blob storage
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = UploadPolicy)]
[Produces("application/json")]
public class DocumentUploadController : ControllerBase
{
    /// <summary>
    /// Authorization policy requiring the delegated SASKey.Write scope
    /// </summary>
    public const string UploadPolicy = "SasKeyWrite";

    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<DocumentUploadController> _logger;

    public DocumentUploadController(
        IBlobStorageService blobStorageService,
        ILogger<DocumentUploadController> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a time-limited SAS token for direct blob upload
    /// </summary>
    /// <remarks>
    /// The returned SAS URI can be used by the client to upload files directly to blob storage.
    /// The token automatically expires after the specified period.
    /// 
    /// Request example:
    /// ```
    /// POST /api/documentupload/sas-token
    /// {
    ///   "fileName": "document.pdf",
    ///   "fileSizeBytes": 1024000,
    ///   "mimeType": "application/pdf"
    /// }
    /// ```
    /// </remarks>
    [HttpPost("sas-token")]
    [ProducesResponseType(typeof(SasTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SasTokenResponse>> GetSasToken(
        [FromBody] SasTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                _logger.LogWarning("SAS token request with empty file name");
                return BadRequest(new ErrorResponse
                {
                    Code = "INVALID_FILE_NAME",
                    Message = "File name is required",
                    RequestId = HttpContext.TraceIdentifier
                });
            }

            // Validate file before generating token
            var validation = _blobStorageService.ValidateFile(request.FileName, request.FileSizeBytes);
            if (!validation.IsValid)
            {
                _logger.LogWarning("File validation failed for {FileName}: {Error}", 
                    request.FileName, validation.ErrorMessage);
                return BadRequest(new ErrorResponse
                {
                    Code = "FILE_VALIDATION_FAILED",
                    Message = validation.ErrorMessage ?? "File validation failed",
                    RequestId = HttpContext.TraceIdentifier
                });
            }

            // Get user identifier from claims
            var userIdentifier = GetUserIdentifier();
            if (string.IsNullOrEmpty(userIdentifier))
            {
                _logger.LogWarning("Could not extract user identifier from token");
                return Unauthorized(new ErrorResponse
                {
                    Code = "USER_NOT_IDENTIFIED",
                    Message = "Unable to identify user from authentication token",
                    RequestId = HttpContext.TraceIdentifier
                });
            }

            // Generate SAS token
            var sasToken = await _blobStorageService.GenerateUploadSasTokenAsync(
                request.FileName,
                userIdentifier,
                cancellationToken);

            _logger.LogInformation("SAS token generated for user {User} and file {File}",
                userIdentifier, request.FileName);

            return Ok(sasToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in SAS token request");
            return BadRequest(new ErrorResponse
            {
                Code = "INVALID_REQUEST",
                Message = ex.Message,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation in SAS token request");
            return BadRequest(new ErrorResponse
            {
                Code = "INVALID_OPERATION",
                Message = ex.Message,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating SAS token");
            return StatusCode(500, new ErrorResponse
            {
                Code = "INTERNAL_ERROR",
                Message = "An unexpected error occurred while generating the SAS token",
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Confirms receipt of an uploaded blob and returns metadata
    /// </summary>
    /// <remarks>
    /// Call this endpoint after successfully uploading a file to blob storage.
    /// It verifies the blob exists and returns upload confirmation details.
    /// </remarks>
    [HttpPost("confirm-upload")]
    [ProducesResponseType(typeof(UploadConfirmationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadConfirmationResponse>> ConfirmUpload(
        [FromQuery] string blobPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return BadRequest(new ErrorResponse
                {
                    Code = "BLOB_PATH_REQUIRED",
                    Message = "Blob path is required",
                    RequestId = HttpContext.TraceIdentifier
                });
            }

            var confirmation = await _blobStorageService.ConfirmUploadAsync(blobPath, cancellationToken);

            _logger.LogInformation("Upload confirmed for blob path {BlobPath} by user {User}",
                blobPath, GetUserIdentifier());

            return Ok(confirmation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming upload for blob path {BlobPath}", blobPath);
            return StatusCode(500, new ErrorResponse
            {
                Code = "CONFIRMATION_ERROR",
                Message = "An error occurred while confirming the upload",
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Health check endpoint for monitoring
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Gets current user information (for debugging)
    /// </summary>
    [HttpGet("user-info")]
    public IActionResult GetUserInfo()
    {
        var userInfo = new
        {
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Email = GetUserIdentifier(),
            Name = User.FindFirst(ClaimTypes.Name)?.Value,
            Claims = User.Claims.Select(c => new { Type = c.Type, Value = c.Value }).ToList()
        };

        return Ok(userInfo);
    }

    /// <summary>
    /// Extracts user identifier from JWT claims
    /// Tries multiple common claim types used by Azure AD/Entra
    /// </summary>
    private string? GetUserIdentifier()
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
            var value = User.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }
}
