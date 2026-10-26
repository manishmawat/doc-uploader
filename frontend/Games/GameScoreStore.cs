using Microsoft.JSInterop;

namespace UploaderDoc.Games;

/// <summary>
/// Best scores kept in the browser's localStorage. Storage can be blocked (private mode,
/// site data disabled), so failures are ignored and the games just don't remember scores.
/// </summary>
public class GameScoreStore(IJSRuntime jsRuntime)
{
    private const string Prefix = "uploaderdoc.games.";

    public async Task<int?> GetAsync(string key)
    {
        try
        {
            var value = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", Prefix + key);
            return int.TryParse(value, out var number) ? number : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetAsync(string key, int value)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", Prefix + key, value.ToString());
        }
        catch (JSException)
        {
        }
    }
}
