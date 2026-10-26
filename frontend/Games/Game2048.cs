namespace UploaderDoc.Games;

public enum MoveDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// 2048 rules: slide all tiles one way, equal neighbours merge once per move,
/// and a new 2 (90%) or 4 (10%) appears after every move that changed the board.
/// </summary>
public class Game2048
{
    public const int Size = 4;
    public const int WinningTile = 2048;

    private readonly Random _random;
    private readonly int[,] _tiles = new int[Size, Size];

    public Game2048(Random? random = null)
    {
        _random = random ?? Random.Shared;
        NewGame();
    }

    public int Score { get; private set; }

    /// <summary>True once a 2048 tile has been made; the player may keep going.</summary>
    public bool HasWon { get; private set; }

    public bool IsGameOver { get; private set; }

    public int this[int row, int col] => _tiles[row, col];

    public void NewGame()
    {
        Array.Clear(_tiles);
        Score = 0;
        HasWon = false;
        IsGameOver = false;
        AddRandomTile();
        AddRandomTile();
    }

    /// <summary>Sets the board directly (rows of values, 0 = empty). For tests.</summary>
    internal void Load(int[][] rows)
    {
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
                _tiles[r, c] = rows[r][c];
        IsGameOver = !CanMove();
    }

    /// <summary>Slides the tiles; returns false (and adds no tile) if nothing moved.</summary>
    public bool Move(MoveDirection direction)
    {
        if (IsGameOver)
            return false;

        var moved = false;
        for (var line = 0; line < Size; line++)
        {
            var cells = LineCells(direction, line);
            var values = cells.Select(cell => _tiles[cell.Row, cell.Col]).ToArray();
            var merged = SlideLine(values, out var gained);

            if (!values.SequenceEqual(merged))
            {
                moved = true;
                Score += gained;
                for (var i = 0; i < Size; i++)
                    _tiles[cells[i].Row, cells[i].Col] = merged[i];
            }
        }

        if (!moved)
            return false;

        if (!HasWon && AllTiles().Any(value => value >= WinningTile))
            HasWon = true;

        AddRandomTile();
        IsGameOver = !CanMove();
        return true;
    }

    /// <summary>
    /// Slides one line towards index 0, merging each pair of equal tiles once.
    /// [2,2,2,2] becomes [4,4,0,0]; [4,0,4,8] becomes [8,8,0,0].
    /// </summary>
    internal static int[] SlideLine(int[] line, out int gained)
    {
        gained = 0;
        var tiles = line.Where(value => value != 0).ToList();
        var result = new List<int>(Size);

        for (var i = 0; i < tiles.Count; i++)
        {
            if (i + 1 < tiles.Count && tiles[i] == tiles[i + 1])
            {
                var value = tiles[i] * 2;
                result.Add(value);
                gained += value;
                i++;
            }
            else
            {
                result.Add(tiles[i]);
            }
        }

        while (result.Count < line.Length)
            result.Add(0);
        return result.ToArray();
    }

    /// <summary>The cells of one row or column, ordered from the edge the tiles slide towards.</summary>
    private static (int Row, int Col)[] LineCells(MoveDirection direction, int line) =>
        Enumerable.Range(0, Size).Select(i => direction switch
        {
            MoveDirection.Left => (line, i),
            MoveDirection.Right => (line, Size - 1 - i),
            MoveDirection.Up => (i, line),
            _ => (Size - 1 - i, line)
        }).ToArray();

    private void AddRandomTile()
    {
        var empty = new List<(int Row, int Col)>();
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
                if (_tiles[r, c] == 0)
                    empty.Add((r, c));

        if (empty.Count == 0)
            return;

        var (row, col) = empty[_random.Next(empty.Count)];
        _tiles[row, col] = _random.Next(10) == 0 ? 4 : 2;
    }

    private bool CanMove()
    {
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
            {
                var value = _tiles[r, c];
                if (value == 0
                    || (c + 1 < Size && _tiles[r, c + 1] == value)
                    || (r + 1 < Size && _tiles[r + 1, c] == value))
                    return true;
            }
        return false;
    }

    private IEnumerable<int> AllTiles() => _tiles.Cast<int>();
}
