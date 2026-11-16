using UploaderDoc.Cards;

namespace UploaderDoc.Tests;

public class BarcodeServiceTests
{
    private readonly BarcodeService service = new();

    [Theory]
    [InlineData("CODE_128", "0041237765")]
    [InlineData("CODE_128", "Member #12-AB")]
    [InlineData("CODE_39", "GYM12345")]
    [InlineData("CODE_93", "GYM12345")]
    [InlineData("CODABAR", "40156")]
    [InlineData("EAN_13", "9781234567897")]
    [InlineData("EAN_8", "96385074")]
    [InlineData("UPC_A", "036000291452")]
    [InlineData("UPC_E", "01234565")]
    [InlineData("ITF", "12345678")]
    [InlineData("QR_CODE", "MEMBER:12345 Fitness Club")]
    [InlineData("PDF_417", "MEMBER-0041237765")]
    [InlineData("DATA_MATRIX", "0041237765")]
    [InlineData("AZTEC", "0041237765")]
    public void Drawn_barcode_reads_back_as_the_same_value(string format, string value)
    {
        var image = service.Encode(format, value);
        var (pixels, width, height) = Rasterize(image, moduleSize: 3);

        var decoded = service.Decode(pixels, width, height);

        Assert.NotNull(decoded);
        Assert.Equal(format, decoded.Format);
        Assert.Equal(value, decoded.Value);
    }

    [Fact]
    public void Decode_reads_a_photo_like_image_with_noise_and_offset()
    {
        var image = service.Encode("CODE_128", "0041237765");
        var (pixels, width, height) = Rasterize(image, moduleSize: 4, padding: 120, noise: 40);

        Assert.Equal(new DecodedBarcode("CODE_128", "0041237765"), service.Decode(pixels, width, height));
    }

    [Fact]
    public void Decode_returns_null_when_there_is_no_barcode()
    {
        var blank = Enumerable.Repeat((byte)230, 400 * 300).ToArray();

        Assert.Null(service.Decode(blank, 400, 300));
    }

    [Theory]
    [InlineData("EAN_13", "9781234567890")] // wrong check digit
    [InlineData("EAN_13", "12AB")]
    [InlineData("ITF", "12345")] // odd length
    [InlineData("UPC_A", "123")]
    [InlineData("CODE_128", "")]
    public void Validate_rejects_values_the_format_cannot_hold(string format, string value)
    {
        Assert.NotNull(service.Validate(format, value));
    }

    [Theory]
    [InlineData("CODE_128", "0041237765")]
    [InlineData("EAN_13", "978123456789")] // check digit added
    [InlineData("QR_CODE", "anything at all")]
    public void Validate_accepts_valid_values(string format, string value)
    {
        Assert.Null(service.Validate(format, value));
    }

    [Fact]
    public void Svg_is_scalable_and_has_a_quiet_zone()
    {
        var svg = service.ToSvg("CODE_128", "0041237765");
        var image = service.Encode("CODE_128", "0041237765");

        Assert.StartsWith("<svg", svg);
        Assert.Contains($"viewBox=\"0 0 {image.Width} {image.Height}\"", svg);
        Assert.Contains("shape-rendering=\"crispEdges\"", svg);
        Assert.Contains("M10 10h", svg); // first bar starts after the 10-module quiet zone
        Assert.Equal(1, image.Rows);
    }

    [Fact]
    public void Square_codes_keep_their_rows()
    {
        var image = service.Encode("QR_CODE", "hello");

        Assert.Equal(image.Columns, image.Rows);
        Assert.Equal(4, image.QuietZone);
        Assert.False(BarcodeFormats.IsLinear("QR_CODE"));
    }

    [Fact]
    public void Every_format_has_a_display_name()
    {
        Assert.Equal(13, BarcodeFormats.All.Count);
        Assert.Equal("Code 128", BarcodeFormats.DisplayName("CODE_128"));
        Assert.Equal("UNKNOWN", BarcodeFormats.DisplayName("UNKNOWN"));
    }

    /// <summary>Draws the image as greyscale pixels the way the SVG renders it.</summary>
    private static (byte[] Pixels, int Width, int Height) Rasterize(BarcodeImage image, int moduleSize, int padding = 0, int noise = 0)
    {
        var width = (int)Math.Ceiling(image.Width * moduleSize) + 2 * padding;
        var height = (int)Math.Ceiling(image.Height * moduleSize) + 2 * padding;
        var pixels = new byte[width * height];
        var random = new Random(1);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var mx = (x - padding) / (double)moduleSize - image.QuietZone;
                var my = ((y - padding) / (double)moduleSize - image.QuietZone) / image.RowHeight;
                var dark = mx >= 0 && my >= 0 && mx < image.Columns && my < image.Rows && image.Dark[(int)my, (int)mx];
                var value = (dark ? 20 : 235) + (noise > 0 ? random.Next(-noise, noise) : 0);
                pixels[y * width + x] = (byte)Math.Clamp(value, 0, 255);
            }

        return (pixels, width, height);
    }
}
