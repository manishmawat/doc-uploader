namespace UploaderDoc.Games;

public enum MinesweeperState
{
    /// <summary>No cell opened yet; mines are placed on the first reveal so it is always safe.</summary>
    Ready,
    Playing,
    Won,
    Lost
}

public record MinesweeperDifficulty(string Name, int Rows, int Cols, int Mines)
{
    public static readonly MinesweeperDifficulty Easy = new("Easy", 9, 9, 10);
    public static readonly MinesweeperDifficulty Medium = new("Medium", 16, 16, 40);
    public static readonly MinesweeperDifficulty Hard = new("Hard", 16, 30, 99);
    public static readonly IReadOnlyList<MinesweeperDifficulty> All = [Easy, Medium, Hard];
}

public class MinesweeperCell
{
    public bool IsMine { get; internal set; }
    public bool IsRevealed { get; internal set; }
    public bool IsFlagged { get; internal set; }

    /// <summary>Number of mines in the 8 neighbouring cells.</summary>
    public int AdjacentMines { get; internal set; }

    /// <summary>The mine that was clicked, shown differently when the game is lost.</summary>
    public bool Exploded { get; internal set; }
}

/// <summary>
/// Minesweeper rules: the first reveal is safe (no mine in the cell or its neighbours),
/// revealing a cell with no adjacent mines opens its neighbours, and the game is won
/// when every non-mine cell is revealed.
/// </summary>
public class Minesweeper
{
    private readonly Random _random;
    private MinesweeperCell[,] _cells = new MinesweeperCell[0, 0];
    private int _revealedCount;
    private int _mineCount;

    public Minesweeper(MinesweeperDifficulty difficulty, Random? random = null)
    {
        _random = random ?? Random.Shared;
        NewGame(difficulty);
    }

    public MinesweeperDifficulty Difficulty { get; private set; } = MinesweeperDifficulty.Easy;
    public MinesweeperState State { get; private set; }
    public int Rows => Difficulty.Rows;
    public int Cols => Difficulty.Cols;
    public int FlagsLeft => Difficulty.Mines - Cells.Count(cell => cell.IsFlagged);
    public bool IsFinished => State is MinesweeperState.Won or MinesweeperState.Lost;

    public MinesweeperCell this[int row, int col] => _cells[row, col];

    private IEnumerable<MinesweeperCell> Cells => _cells.Cast<MinesweeperCell>();

    public void NewGame(MinesweeperDifficulty difficulty)
    {
        Difficulty = difficulty;
        _cells = new MinesweeperCell[difficulty.Rows, difficulty.Cols];
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                _cells[r, c] = new MinesweeperCell();
        _revealedCount = 0;
        State = MinesweeperState.Ready;
    }

    /// <summary>Opens a cell. Flagged and already-open cells are ignored.</summary>
    public void Reveal(int row, int col)
    {
        if (IsFinished || _cells[row, col].IsFlagged || _cells[row, col].IsRevealed)
            return;

        if (State == MinesweeperState.Ready)
        {
            var safe = Neighbours(row, col).Append((row, col)).ToHashSet();
            var candidates = AllPositions().Where(p => !safe.Contains(p)).ToList();
            PlaceMines(candidates.OrderBy(_ => _random.Next()).Take(Difficulty.Mines));
        }

        var cell = _cells[row, col];
        if (cell.IsMine)
        {
            cell.Exploded = true;
            State = MinesweeperState.Lost;
            foreach (var mine in Cells.Where(c => c.IsMine))
                mine.IsRevealed = true;
            return;
        }

        // Flood fill: opening a cell with no adjacent mines opens its neighbours too
        var pending = new Stack<(int Row, int Col)>();
        pending.Push((row, col));
        while (pending.Count > 0)
        {
            var (r, c) = pending.Pop();
            var current = _cells[r, c];
            if (current.IsRevealed || current.IsFlagged || current.IsMine)
                continue;

            current.IsRevealed = true;
            _revealedCount++;
            if (current.AdjacentMines == 0)
                foreach (var neighbour in Neighbours(r, c))
                    pending.Push(neighbour);
        }

        if (_revealedCount == Rows * Cols - _mineCount)
        {
            State = MinesweeperState.Won;
            foreach (var mine in Cells.Where(c => c.IsMine))
                mine.IsFlagged = true;
        }
    }

    public void ToggleFlag(int row, int col)
    {
        var cell = _cells[row, col];
        if (IsFinished || cell.IsRevealed)
            return;
        cell.IsFlagged = !cell.IsFlagged;
    }

    /// <summary>Places mines at the given positions and starts play. Used on first reveal, and by tests.</summary>
    internal void PlaceMines(IEnumerable<(int Row, int Col)> positions)
    {
        foreach (var (r, c) in positions)
            _cells[r, c].IsMine = true;
        _mineCount = Cells.Count(cell => cell.IsMine);

        foreach (var (r, c) in AllPositions())
            _cells[r, c].AdjacentMines = Neighbours(r, c).Count(n => _cells[n.Row, n.Col].IsMine);

        State = MinesweeperState.Playing;
    }

    private IEnumerable<(int Row, int Col)> AllPositions()
    {
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                yield return (r, c);
    }

    private IEnumerable<(int Row, int Col)> Neighbours(int row, int col)
    {
        for (var dr = -1; dr <= 1; dr++)
            for (var dc = -1; dc <= 1; dc++)
            {
                var (r, c) = (row + dr, col + dc);
                if ((dr != 0 || dc != 0) && r >= 0 && r < Rows && c >= 0 && c < Cols)
                    yield return (r, c);
            }
    }
}
