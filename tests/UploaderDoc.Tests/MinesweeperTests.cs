using UploaderDoc.Games;

namespace UploaderDoc.Tests;

public class MinesweeperTests
{
    [Theory]
    [MemberData(nameof(Difficulties))]
    public void First_reveal_is_always_safe(MinesweeperDifficulty difficulty)
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var game = new Minesweeper(difficulty, new Random(seed));
            var (row, col) = (difficulty.Rows / 2, difficulty.Cols / 2);

            game.Reveal(row, col);

            Assert.NotEqual(MinesweeperState.Lost, game.State);
            Assert.Equal(0, game[row, col].AdjacentMines); // its neighbours are kept clear too
            Assert.Equal(difficulty.Mines, Count(game, c => c.IsMine));
        }
    }

    [Fact]
    public void Adjacent_counts_are_correct()
    {
        var game = new Minesweeper(MinesweeperDifficulty.Easy);
        game.PlaceMines([(0, 0), (0, 1), (2, 2)]);

        Assert.Equal(2, game[1, 0].AdjacentMines);     // (0,0), (0,1)
        Assert.Equal(3, game[1, 1].AdjacentMines);     // (0,0), (0,1), (2,2)
        Assert.Equal(1, game[3, 3].AdjacentMines);     // (2,2)
        Assert.Equal(0, game[8, 8].AdjacentMines);
    }

    [Fact]
    public void Revealing_an_empty_cell_opens_its_area()
    {
        var game = new Minesweeper(MinesweeperDifficulty.Easy);
        game.PlaceMines([(0, 0)]);

        game.Reveal(8, 8);

        // Everything except the mine opens, which wins the game
        Assert.Equal(9 * 9 - 1, Count(game, c => c.IsRevealed));
        Assert.Equal(MinesweeperState.Won, game.State);
        Assert.True(game[0, 0].IsFlagged); // remaining mines are flagged on a win
    }

    [Fact]
    public void Revealing_a_numbered_cell_opens_only_that_cell()
    {
        var game = new Minesweeper(MinesweeperDifficulty.Easy);
        game.PlaceMines([(0, 0), (4, 4)]);

        game.Reveal(3, 3);

        Assert.True(game[3, 3].IsRevealed);
        Assert.Equal(1, Count(game, c => c.IsRevealed));
        Assert.Equal(MinesweeperState.Playing, game.State);
    }

    [Fact]
    public void Revealing_a_mine_loses_and_shows_all_mines()
    {
        var game = new Minesweeper(MinesweeperDifficulty.Easy);
        game.PlaceMines([(0, 0), (8, 8)]);

        game.Reveal(0, 0);

        Assert.Equal(MinesweeperState.Lost, game.State);
        Assert.True(game[0, 0].Exploded);
        Assert.True(game[8, 8].IsRevealed);
    }

    [Fact]
    public void Flags_block_reveal_and_count_down()
    {
        var game = new Minesweeper(MinesweeperDifficulty.Easy);
        game.PlaceMines([(0, 0)]);

        game.ToggleFlag(0, 0);
        game.Reveal(0, 0);

        Assert.Equal(MinesweeperState.Playing, game.State);
        Assert.Equal(9, game.FlagsLeft);

        game.ToggleFlag(0, 0);
        Assert.Equal(10, game.FlagsLeft);
    }

    [Fact]
    public void Flood_fill_does_not_open_flagged_cells()
    {
        var game = new Minesweeper(MinesweeperDifficulty.Easy);
        game.PlaceMines([(0, 0)]);
        game.ToggleFlag(5, 5);

        game.Reveal(8, 8);

        Assert.False(game[5, 5].IsRevealed);
        Assert.Equal(MinesweeperState.Playing, game.State);
    }

    public static TheoryData<MinesweeperDifficulty> Difficulties() =>
        new(MinesweeperDifficulty.All);

    private static int Count(Minesweeper game, Func<MinesweeperCell, bool> predicate)
    {
        var count = 0;
        for (var r = 0; r < game.Rows; r++)
            for (var c = 0; c < game.Cols; c++)
                if (predicate(game[r, c]))
                    count++;
        return count;
    }
}
