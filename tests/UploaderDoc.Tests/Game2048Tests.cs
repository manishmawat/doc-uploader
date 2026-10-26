using UploaderDoc.Games;

namespace UploaderDoc.Tests;

public class Game2048Tests
{
    [Theory]
    [InlineData(new[] { 2, 2, 0, 0 }, new[] { 4, 0, 0, 0 }, 4)]
    [InlineData(new[] { 2, 2, 2, 2 }, new[] { 4, 4, 0, 0 }, 8)]
    [InlineData(new[] { 4, 0, 4, 8 }, new[] { 8, 8, 0, 0 }, 8)]
    [InlineData(new[] { 2, 2, 4, 0 }, new[] { 4, 4, 0, 0 }, 4)] // merged tile doesn't merge again
    [InlineData(new[] { 0, 0, 0, 2 }, new[] { 2, 0, 0, 0 }, 0)]
    [InlineData(new[] { 2, 4, 8, 16 }, new[] { 2, 4, 8, 16 }, 0)]
    public void SlideLine_merges_each_pair_once(int[] line, int[] expected, int expectedGain)
    {
        var result = Game2048.SlideLine(line, out var gained);

        Assert.Equal(expected, result);
        Assert.Equal(expectedGain, gained);
    }

    [Fact]
    public void New_game_starts_with_two_tiles()
    {
        var game = new Game2048(new Random(1));

        Assert.Equal(2, Tiles(game).Count(v => v != 0));
        Assert.All(Tiles(game).Where(v => v != 0), v => Assert.Contains(v, new[] { 2, 4 }));
    }

    [Theory]
    [InlineData(MoveDirection.Left, 0, 0)]
    [InlineData(MoveDirection.Right, 0, 3)]
    [InlineData(MoveDirection.Up, 0, 1)]
    [InlineData(MoveDirection.Down, 3, 1)]
    public void Move_slides_merges_scores_and_adds_a_tile(MoveDirection direction, int row, int col)
    {
        var game = new Game2048(new Random(1));
        // Two 2s that meet whichever way they slide: (0,1) and (0,2) for left/right, (0,1) and (1,1) for up/down
        var board = direction is MoveDirection.Left or MoveDirection.Right
            ? new[] { new[] { 0, 2, 2, 0 }, new int[4], new int[4], new int[4] }
            : new[] { new[] { 0, 2, 0, 0 }, new[] { 0, 2, 0, 0 }, new int[4], new int[4] };
        game.Load(board);

        Assert.True(game.Move(direction));

        Assert.Equal(4, game[row, col]);
        Assert.Equal(4, game.Score);
        Assert.Equal(2, Tiles(game).Count(v => v != 0)); // merged tile + the new one
    }

    [Fact]
    public void Move_that_changes_nothing_adds_no_tile()
    {
        var game = new Game2048(new Random(1));
        game.Load([[2, 4, 0, 0], new int[4], new int[4], new int[4]]);

        Assert.False(game.Move(MoveDirection.Left));
        Assert.Equal(2, Tiles(game).Count(v => v != 0));
    }

    [Fact]
    public void Making_2048_wins_but_play_can_continue()
    {
        var game = new Game2048(new Random(1));
        game.Load([[1024, 1024, 0, 0], new int[4], new int[4], new int[4]]);

        Assert.True(game.Move(MoveDirection.Left));

        Assert.True(game.HasWon);
        Assert.False(game.IsGameOver);
        Assert.Equal(2048, game[0, 0]);
    }

    [Fact]
    public void Full_board_with_no_merges_is_game_over()
    {
        var game = new Game2048(new Random(1));
        game.Load([[2, 4, 2, 4], [4, 2, 4, 2], [2, 4, 2, 4], [4, 2, 4, 2]]);

        Assert.True(game.IsGameOver);
        Assert.False(game.Move(MoveDirection.Left));
    }

    private static IEnumerable<int> Tiles(Game2048 game) =>
        from r in Enumerable.Range(0, Game2048.Size)
        from c in Enumerable.Range(0, Game2048.Size)
        select game[r, c];
}
