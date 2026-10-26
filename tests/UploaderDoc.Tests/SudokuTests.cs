using UploaderDoc.Games;

namespace UploaderDoc.Tests;

public class SudokuTests
{
    private const string Puzzle =
        "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
    private const string Solution =
        "534678912672195348198342567859761423426853791713924856961537284287419635345286179";

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void Generated_puzzles_have_one_solution_and_the_requested_clues(SudokuDifficulty difficulty)
    {
        for (var seed = 0; seed < 5; seed++)
        {
            var solution = Sudoku.GenerateSolution(new Random(seed));
            Assert.True(IsValidSolution(solution));

            var puzzle = Sudoku.RemoveClues(solution, difficulty.Givens, new Random(seed));

            Assert.Equal(1, Sudoku.CountSolutions(puzzle));
            Assert.Equal(difficulty.Givens, puzzle.Count(v => v != 0));
            Assert.All(Enumerable.Range(0, Sudoku.CellCount),
                i => Assert.True(puzzle[i] == 0 || puzzle[i] == solution[i]));
        }
    }

    [Fact]
    public void CountSolutions_detects_none_one_and_many()
    {
        Assert.Equal(1, Sudoku.CountSolutions(Digits(Puzzle)));
        Assert.Equal(2, Sudoku.CountSolutions(new int[Sudoku.CellCount])); // empty grid, capped at 2
        var conflicting = Digits(Puzzle);
        conflicting[2] = 5; // second 5 in row 0
        Assert.Equal(0, Sudoku.CountSolutions(conflicting));
    }

    [Fact]
    public void Givens_cannot_be_changed()
    {
        var game = Load();

        game.SetValue(0, 9);

        Assert.Equal(5, game.Value(0));
        Assert.True(game.IsGiven(0));
    }

    [Fact]
    public void Conflicts_are_detected_in_row_column_and_box()
    {
        var game = Load();

        game.SetValue(2, 5); // row 0 already has 5 at index 0
        Assert.True(game.HasConflict(2));
        Assert.True(game.HasConflict(0));

        game.SetValue(2, 4); // correct digit
        Assert.False(game.HasConflict(2));
        Assert.False(game.HasConflict(0));
    }

    [Fact]
    public void Placing_a_digit_clears_matching_notes_in_peers_and_undo_restores_them()
    {
        var game = Load();
        game.ToggleNote(3, 4);  // same row as index 2
        game.ToggleNote(10, 4); // same box as index 2
        game.ToggleNote(80, 4); // unrelated cell (already given, so ignored)
        game.ToggleNote(78, 4); // unrelated empty cell

        game.SetValue(2, 4);

        Assert.False(game.HasNote(3, 4));
        Assert.False(game.HasNote(10, 4));
        Assert.True(game.HasNote(78, 4));

        game.Undo();

        Assert.Equal(0, game.Value(2));
        Assert.True(game.HasNote(3, 4));
        Assert.True(game.HasNote(10, 4));
    }

    [Fact]
    public void Notes_only_go_on_empty_cells()
    {
        var game = Load();
        game.SetValue(2, 4);

        game.ToggleNote(2, 7);
        game.ToggleNote(0, 7); // given

        Assert.False(game.HasNote(2, 7));
        Assert.False(game.HasNote(0, 7));
    }

    [Fact]
    public void Hint_fills_the_selected_cell_or_the_first_unsolved_one()
    {
        var game = Load();
        game.SetValue(2, 9); // wrong

        Assert.Equal(2, game.Hint(2));
        Assert.Equal(4, game.Value(2));
        Assert.True(game.IsHinted(2));

        Assert.Equal(3, game.Hint()); // index 3 is the first empty cell
        Assert.Equal(6, game.Value(3));
        Assert.Equal(2, game.HintsUsed);
    }

    [Fact]
    public void Filling_every_cell_correctly_solves_the_puzzle()
    {
        var game = Load();
        var solution = Digits(Solution);

        for (var i = 0; i < Sudoku.CellCount; i++)
            game.SetValue(i, solution[i]);

        Assert.True(game.IsSolved);
        Assert.Null(game.Hint());
        Assert.All(game.DigitCounts().Skip(1), count => Assert.Equal(9, count));
    }

    [Fact]
    public void Peers_are_the_20_cells_sharing_a_unit()
    {
        var peers = Sudoku.Peers(40).ToHashSet(); // centre cell

        Assert.Equal(20, peers.Count);
        Assert.Contains(36, peers); // same row
        Assert.Contains(4, peers);  // same column
        Assert.Contains(30, peers); // same box
        Assert.DoesNotContain(0, peers);
    }

    public static TheoryData<SudokuDifficulty> Difficulties() => new(SudokuDifficulty.All);

    private static Sudoku Load()
    {
        var game = new Sudoku(SudokuDifficulty.Easy, new Random(1));
        game.Load(Puzzle, Solution);
        return game;
    }

    private static int[] Digits(string s) => s.Select(ch => ch - '0').ToArray();

    private static bool IsValidSolution(int[] grid)
    {
        for (var i = 0; i < Sudoku.CellCount; i++)
            if (grid[i] is < 1 or > 9 || Sudoku.Peers(i).Any(p => grid[p] == grid[i]))
                return false;
        return true;
    }
}
