namespace DocUploaderApi.Models;

/// <summary>
/// A membership or loyalty card saved by the user: a name and the barcode it shows.
/// Only the barcode's type and value are stored; the app redraws the barcode from them.
/// </summary>
public class BarcodeCard
{
    public string Id { get; set; } = string.Empty;

    /// <summary>What the card is for, e.g. "Fitness club"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Barcode type, one of <see cref="CardRules.Formats"/> (e.g. CODE_128, QR_CODE)</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>The text encoded in the barcode</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Tile colour as #rrggbb</summary>
    public string Color { get; set; } = CardRules.DefaultColor;

    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }

    /// <summary>When the card's photo was last set; null when the card has no photo.
    /// Clients use it to tell whether their offline copy of the photo is current.</summary>
    public DateTimeOffset? PhotoUpdatedOn { get; set; }
}

/// <summary>
/// Request body for creating or updating a card
/// </summary>
public class BarcodeCardRequest
{
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public static class CardRules
{
    public const int MaxCards = 50;
    public const int MaxNameLength = 40;
    public const int MaxValueLength = 500;
    public const string DefaultColor = "#0f6cbd";

    /// <summary>The app shrinks photos to about 100-200 KB before upload; this leaves plenty of room</summary>
    public const long MaxPhotoBytes = 2 * 1024 * 1024;

    public static readonly IReadOnlySet<string> PhotoContentTypes = new HashSet<string>
    {
        "image/jpeg", "image/png", "image/webp"
    };

    /// <summary>Barcode types the app can read and draw (ZXing names)</summary>
    public static readonly IReadOnlySet<string> Formats = new HashSet<string>
    {
        "CODE_128", "CODE_39", "CODE_93", "CODABAR", "EAN_13", "EAN_8", "UPC_A", "UPC_E", "ITF",
        "QR_CODE", "PDF_417", "DATA_MATRIX", "AZTEC"
    };

    /// <summary>Returns an error message, or null when the request is valid</summary>
    public static string? Validate(BarcodeCardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required";
        if (request.Name.Trim().Length > MaxNameLength)
            return $"Name can be at most {MaxNameLength} characters";
        if (string.IsNullOrWhiteSpace(request.Value))
            return "Barcode value is required";
        if (request.Value.Length > MaxValueLength)
            return $"Barcode value can be at most {MaxValueLength} characters";
        if (!Formats.Contains(request.Format))
            return $"Unsupported barcode type '{request.Format}'";
        if (request.Color is not null && !System.Text.RegularExpressions.Regex.IsMatch(request.Color, "^#[0-9a-fA-F]{6}$"))
            return "Color must be #rrggbb";
        return null;
    }
}
