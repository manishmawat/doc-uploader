namespace UploaderDoc.Games;

public enum FlipResult
{
    /// <summary>The card can't be flipped now (already up or matched, or two cards are showing).</summary>
    Ignored,
    /// <summary>First card of a turn.</summary>
    FirstCard,
    Match,
    /// <summary>The two face-up cards differ; call <see cref="MemoryMatch.HideMismatch"/> after showing them.</summary>
    Mismatch
}

public class MemoryCard
{
    /// <summary>Which picture the card shows; two cards share each symbol.</summary>
    public int Symbol { get; init; }
    public bool IsFaceUp { get; internal set; }
    public bool IsMatched { get; internal set; }
}

/// <summary>
/// Memory match rules: flip two cards per turn; equal symbols stay up, different ones are
/// turned back. A move is one pair of flips.
/// </summary>
public class MemoryMatch
{
    private readonly Random _random;
    private readonly List<MemoryCard> _cards = [];
    private int? _firstIndex;
    private (int First, int Second)? _mismatch;

    public MemoryMatch(int pairs, Random? random = null)
    {
        _random = random ?? Random.Shared;
        Pairs = pairs;
        NewGame();
    }

    public int Pairs { get; }
    public int Moves { get; private set; }
    public int MatchedPairs { get; private set; }
    public bool IsComplete => MatchedPairs == Pairs;

    /// <summary>True while a mismatched pair is face up, waiting for <see cref="HideMismatch"/>.</summary>
    public bool IsShowingMismatch => _mismatch is not null;

    public IReadOnlyList<MemoryCard> Cards => _cards;

    public void NewGame()
    {
        _cards.Clear();
        _cards.AddRange(Enumerable.Range(0, Pairs)
            .SelectMany(symbol => new[] { new MemoryCard { Symbol = symbol }, new MemoryCard { Symbol = symbol } })
            .OrderBy(_ => _random.Next()));
        _firstIndex = null;
        _mismatch = null;
        Moves = 0;
        MatchedPairs = 0;
    }

    public FlipResult Flip(int index)
    {
        var card = _cards[index];
        if (IsShowingMismatch || card.IsFaceUp || card.IsMatched)
            return FlipResult.Ignored;

        card.IsFaceUp = true;

        if (_firstIndex is not { } firstIndex)
        {
            _firstIndex = index;
            return FlipResult.FirstCard;
        }

        _firstIndex = null;
        Moves++;
        var first = _cards[firstIndex];

        if (first.Symbol == card.Symbol)
        {
            first.IsMatched = card.IsMatched = true;
            MatchedPairs++;
            return FlipResult.Match;
        }

        _mismatch = (firstIndex, index);
        return FlipResult.Mismatch;
    }

    /// <summary>Turns a mismatched pair face down again.</summary>
    public void HideMismatch()
    {
        if (_mismatch is not { } pair)
            return;
        _cards[pair.First].IsFaceUp = false;
        _cards[pair.Second].IsFaceUp = false;
        _mismatch = null;
    }
}
