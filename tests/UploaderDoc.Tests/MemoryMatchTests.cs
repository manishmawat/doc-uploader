using UploaderDoc.Games;

namespace UploaderDoc.Tests;

public class MemoryMatchTests
{
    [Fact]
    public void Deck_has_two_of_each_symbol()
    {
        var game = new MemoryMatch(8, new Random(1));

        Assert.Equal(16, game.Cards.Count);
        Assert.All(game.Cards.GroupBy(c => c.Symbol), g => Assert.Equal(2, g.Count()));
    }

    [Fact]
    public void Matching_pair_stays_face_up()
    {
        var game = new MemoryMatch(8, new Random(1));
        var (a, b) = IndexesOf(game, symbol: 0);

        Assert.Equal(FlipResult.FirstCard, game.Flip(a));
        Assert.Equal(FlipResult.Match, game.Flip(b));

        Assert.True(game.Cards[a].IsMatched && game.Cards[b].IsMatched);
        Assert.Equal(1, game.Moves);
        Assert.Equal(1, game.MatchedPairs);
    }

    [Fact]
    public void Mismatch_blocks_flips_until_hidden()
    {
        var game = new MemoryMatch(8, new Random(1));
        var (a, _) = IndexesOf(game, symbol: 0);
        var (c, d) = IndexesOf(game, symbol: 1);

        game.Flip(a);
        Assert.Equal(FlipResult.Mismatch, game.Flip(c));
        Assert.True(game.IsShowingMismatch);
        Assert.Equal(FlipResult.Ignored, game.Flip(d));

        game.HideMismatch();

        Assert.False(game.Cards[a].IsFaceUp);
        Assert.False(game.Cards[c].IsFaceUp);
        Assert.Equal(1, game.Moves);
    }

    [Fact]
    public void Flipping_the_same_card_twice_is_ignored()
    {
        var game = new MemoryMatch(8, new Random(1));

        game.Flip(0);

        Assert.Equal(FlipResult.Ignored, game.Flip(0));
        Assert.Equal(0, game.Moves);
    }

    [Fact]
    public void Matching_every_pair_completes_the_game()
    {
        var game = new MemoryMatch(8, new Random(1));

        for (var symbol = 0; symbol < 8; symbol++)
        {
            var (a, b) = IndexesOf(game, symbol);
            game.Flip(a);
            game.Flip(b);
        }

        Assert.True(game.IsComplete);
        Assert.Equal(8, game.Moves);
    }

    private static (int, int) IndexesOf(MemoryMatch game, int symbol)
    {
        var indexes = game.Cards.Select((card, i) => (card, i)).Where(x => x.card.Symbol == symbol).Select(x => x.i).ToArray();
        return (indexes[0], indexes[1]);
    }
}
