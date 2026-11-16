using System.Globalization;
using System.Text;
using ZXing;
using ZXing.Common;

namespace UploaderDoc.Cards;

/// <summary>A barcode found in a photo, with its ZXing format name (e.g. CODE_128).</summary>
public record DecodedBarcode(string Format, string Value);

/// <summary>Barcode types a card can use. Ids match ZXing's names and the API's CardRules.Formats.</summary>
public static class BarcodeFormats
{
    public static readonly IReadOnlyList<(string Id, string Name)> All =
    [
        ("CODE_128", "Code 128"),
        ("CODE_39", "Code 39"),
        ("CODE_93", "Code 93"),
        ("CODABAR", "Codabar"),
        ("EAN_13", "EAN-13"),
        ("EAN_8", "EAN-8"),
        ("UPC_A", "UPC-A"),
        ("UPC_E", "UPC-E"),
        ("ITF", "ITF (Interleaved 2 of 5)"),
        ("QR_CODE", "QR code"),
        ("PDF_417", "PDF417"),
        ("DATA_MATRIX", "Data Matrix"),
        ("AZTEC", "Aztec"),
    ];

    public static string DisplayName(string id) =>
        All.FirstOrDefault(f => f.Id == id).Name ?? id;

    /// <summary>One-dimensional (bars) as opposed to 2D (QR, Data Matrix, Aztec, PDF417).</summary>
    public static bool IsLinear(string id) => id is not ("QR_CODE" or "DATA_MATRIX" or "AZTEC" or "PDF_417");

    internal static BarcodeFormat ToZxing(string id) => Enum.Parse<BarcodeFormat>(id);

    internal static readonly IList<BarcodeFormat> Supported = All.Select(f => ToZxing(f.Id)).ToList();
}

/// <summary>
/// The modules of a barcode ready to draw: <see cref="Dark"/>[row, column], with each row
/// <see cref="RowHeight"/> modules tall and a blank <see cref="QuietZone"/> (in modules) around it.
/// </summary>
public record BarcodeImage(bool[,] Dark, double RowHeight, int QuietZone)
{
    public int Columns => Dark.GetLength(1);
    public int Rows => Dark.GetLength(0);
    public double Width => Columns + 2 * QuietZone;
    public double Height => Rows * RowHeight + 2 * QuietZone;
}

/// <summary>
/// Reads barcodes from photos and draws them as SVG, using ZXing.Net. Runs in the browser,
/// so it works offline.
/// </summary>
public class BarcodeService
{
    /// <summary>Linear barcodes are drawn this many modules tall (about a third of a typical width).</summary>
    private const double LinearHeight = 40;

    /// <summary>
    /// Finds a barcode in a greyscale image (one byte per pixel, row by row). Returns null if none is found.
    /// </summary>
    public DecodedBarcode? Decode(byte[] luminance, int width, int height)
    {
        // Photos are large and often grainy: if the full image fails, retry on half-size copies,
        // where averaging 2x2 blocks smooths the noise
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (DecodeOnce(luminance, width, height) is { } found)
                return found;
            if (width < MinRetrySize * 2 || height < MinRetrySize * 2)
                break;
            (luminance, width, height) = HalfSize(luminance, width, height);
        }
        return null;
    }

    private const int MinRetrySize = 120;

    private static DecodedBarcode? DecodeOnce(byte[] luminance, int width, int height)
    {
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = BarcodeFormats.Supported,
            },
        };
        var source = new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        var result = reader.Decode(source);
        return result is null ? null : new DecodedBarcode(result.BarcodeFormat.ToString(), result.Text);
    }

    /// <summary>Halves the image in both directions, averaging each 2x2 block.</summary>
    internal static (byte[] Pixels, int Width, int Height) HalfSize(byte[] pixels, int width, int height)
    {
        var (w, h) = (width / 2, height / 2);
        var result = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = 2 * y * width + 2 * x;
                result[y * w + x] = (byte)((pixels[i] + pixels[i + 1] + pixels[i + width] + pixels[i + width + 1] + 2) / 4);
            }
        return (result, w, h);
    }

    /// <summary>Returns why <paramref name="value"/> can't be drawn as <paramref name="format"/>, or null if it can.</summary>
    public string? Validate(string format, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Enter the number or text from the card.";
        try
        {
            Encode(format, value);
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or WriterException or System.FormatException or InvalidOperationException)
        {
            return format switch
            {
                "EAN_13" => "An EAN-13 barcode needs 12 or 13 digits (with a valid check digit).",
                "EAN_8" => "An EAN-8 barcode needs 7 or 8 digits (with a valid check digit).",
                "UPC_A" => "A UPC-A barcode needs 11 or 12 digits (with a valid check digit).",
                "UPC_E" => "A UPC-E barcode needs 7 or 8 digits starting with 0 or 1.",
                "ITF" => "An ITF barcode needs an even number of digits.",
                _ => $"This value can't be shown as {BarcodeFormats.DisplayName(format)}.",
            };
        }
    }

    /// <summary>Lays out <paramref name="value"/> as the modules of a <paramref name="format"/> barcode.</summary>
    public BarcodeImage Encode(string format, string value)
    {
        var linear = BarcodeFormats.IsLinear(format);
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 0,
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
        };
        // Smallest size: one pixel per module (linear codes are one row)
        var matrix = new MultiFormatWriter().encode(value, BarcodeFormats.ToZxing(format), 1, 1, hints);

        var rows = linear ? 1 : matrix.Height;
        var dark = new bool[rows, matrix.Width];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < matrix.Width; c++)
                dark[r, c] = matrix[c, r];

        // Scanners need a blank margin: about 10 modules for bars, 4 for 2D codes
        return new BarcodeImage(dark, linear ? LinearHeight : 1, linear ? 10 : 4);
    }

    /// <summary>Draws the barcode as a scalable SVG (crisp edges, white background).</summary>
    public string ToSvg(string format, string value)
    {
        var image = Encode(format, value);
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {image.Width} {image.Height}\" preserveAspectRatio=\"xMidYMid meet\" shape-rendering=\"crispEdges\" role=\"img\">");
        svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{image.Width}\" height=\"{image.Height}\" fill=\"#fff\"/>");
        svg.Append("<path fill=\"#000\" d=\"");

        for (var r = 0; r < image.Rows; r++)
        {
            var y = image.QuietZone + r * image.RowHeight;
            for (var c = 0; c < image.Columns; c++)
            {
                if (!image.Dark[r, c])
                    continue;
                var start = c;
                while (c + 1 < image.Columns && image.Dark[r, c + 1])
                    c++;
                // One rectangle per run of dark modules
                svg.Append(CultureInfo.InvariantCulture,
                    $"M{image.QuietZone + start} {y}h{c - start + 1}v{image.RowHeight}h{-(c - start + 1)}z");
            }
        }

        svg.Append("\"/></svg>");
        return svg.ToString();
    }
}
