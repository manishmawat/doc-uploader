using System.Numerics;

namespace UploaderDoc.Games;

public record SudokuDifficulty(string Name, int Givens)
{
    public static readonly SudokuDifficulty Easy = new("Easy", 40);
    public static readonly SudokuDifficulty Medium = new("Medium", 32);
    public static readonly SudokuDifficulty Hard = new("Hard", 26);
    public static readonly IReadOnlyList<SudokuDifficulty> All = [Easy, Medium, Hard];
}

/// <summary>
/// Sudoku rules and puzzle generation. Cells are indexed 0..80, row by row.
/// Generated puzzles always have exactly one solution. Values are 1..9, 0 means empty;
/// notes are a bitmask per cell (bit n = pencil mark n).
/// </summary>
public class Sudoku
{
    public const int Size = 9;
    public const int CellCount = Size * Size;
    private const int AllDigits = 0b11_1111_1110; // bits 1..9

    private readonly Random _random;
    private readonly int[] _solution = new int[CellCount];
    private readonly int[] _values = new int[CellCount];
    private readonly int[] _notes = new int[CellCount];
    private readonly bool[] _given = new bool[CellCount];
    private readonly bool[] _hinted = new bool[CellCount];
    private readonly Stack<(int Index, int Value, int Notes)[]> _undo = new();

    public Sudoku(SudokuDifficulty difficulty, Random? random = null)
    {
        _random = random ?? Random.Shared;
        NewGame(difficulty);
    }

    public SudokuDifficulty Difficulty { get; private set; } = SudokuDifficulty.Easy;
    public int HintsUsed { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool IsSolved => _values.AsSpan().SequenceEqual(_solution);

    public int Value(int index) => _values[index];
    public bool IsGiven(int index) => _given[index];

    /// <summary>True when the cell shows a digit filled in by <see cref="Hint"/>.</summary>
    public bool IsHinted(int index) => _hinted[index] && _values[index] == _solution[index];
    public bool HasNote(int index, int digit) => (_notes[index] & (1 << digit)) != 0;

    public void NewGame(SudokuDifficulty difficulty)
    {
        Difficulty = difficulty;
        var solution = GenerateSolution(_random);
        var puzzle = RemoveClues(solution, difficulty.Givens, _random);
        Start(puzzle, solution);
    }

    /// <summary>Starts a known puzzle (81 digits, 0 or '.' for blanks). For tests.</summary>
    internal void Load(string puzzle, string solution) =>
        Start(Parse(puzzle), Parse(solution));

    /// <summary>Sets or clears (0) a cell. Placing a digit removes that digit's notes from the row, column and box.</summary>
    public void SetValue(int index, int value)
    {
        if (_given[index] || IsSolved || _values[index] == value)
            return;

        var changes = new List<(int, int, int)> { Snapshot(index) };
        if (value != 0)
        {
            foreach (var peer in Peers(index).Where(p => HasNote(p, value)))
                changes.Add(Snapshot(peer));
        }
        _undo.Push(changes.ToArray());

        _values[index] = value;
        _notes[index] = 0;
        if (value != 0)
            foreach (var peer in Peers(index))
                _notes[peer] &= ~(1 << value);
    }

    /// <summary>Adds or removes a pencil mark on an empty cell.</summary>
    public void ToggleNote(int index, int digit)
    {
        if (_given[index] || _values[index] != 0 || IsSolved)
            return;
        _undo.Push([Snapshot(index)]);
        _notes[index] ^= 1 << digit;
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var changes))
            return;
        foreach (var (index, value, notes) in changes)
        {
            _values[index] = value;
            _notes[index] = notes;
        }
    }

    /// <summary>
    /// Fills a cell with its correct digit: <paramref name="index"/> if it is empty or wrong,
    /// otherwise the first such cell. Returns the cell filled, or null if there is none.
    /// </summary>
    public int? Hint(int? index = null)
    {
        var target = index is { } i && !_given[i] && _values[i] != _solution[i]
            ? i
            : Enumerable.Range(0, CellCount).Cast<int?>().FirstOrDefault(c => _values[c!.Value] != _solution[c.Value]);
        if (target is not { } cell)
            return null;

        SetValue(cell, _solution[cell]);
        _hinted[cell] = true;
        HintsUsed++;
        return cell;
    }

    /// <summary>True when the cell's digit also appears in its row, column or box.</summary>
    public bool HasConflict(int index)
    {
        var value = _values[index];
        return value != 0 && Peers(index).Any(p => _values[p] == value);
    }

    /// <summary>How many times each digit 1..9 is placed (index 0 unused); a count of 9 means that digit is done.</summary>
    public int[] DigitCounts()
    {
        var counts = new int[Size + 1];
        foreach (var value in _values)
            counts[value]++;
        return counts;
    }

    /// <summary>The 20 cells sharing a row, column or 3x3 box with <paramref name="index"/>.</summary>
    public static IEnumerable<int> Peers(int index)
    {
        var (row, col) = (index / Size, index % Size);
        var (boxRow, boxCol) = (row / 3 * 3, col / 3 * 3);
        for (var i = 0; i < CellCount; i++)
        {
            if (i == index)
                continue;
            var (r, c) = (i / Size, i % Size);
            if (r == row || c == col || (r / 3 * 3 == boxRow && c / 3 * 3 == boxCol))
                yield return i;
        }
    }

    /// <summary>Counts solutions of <paramref name="grid"/>, stopping at <paramref name="limit"/>.</summary>
    internal static int CountSolutions(int[] grid, int limit = 2)
    {
        var state = SolverState.From(grid);
        return state is null ? 0 : Search(grid.ToArray(), state.Value, limit, random: null);
    }

    private void Start(int[] puzzle, int[] solution)
    {
        Array.Copy(solution, _solution, CellCount);
        Array.Copy(puzzle, _values, CellCount);
        Array.Clear(_notes);
        Array.Clear(_hinted);
        for (var i = 0; i < CellCount; i++)
            _given[i] = puzzle[i] != 0;
        _undo.Clear();
        HintsUsed = 0;
    }

    private (int, int, int) Snapshot(int index) => (index, _values[index], _notes[index]);

    internal static int[] GenerateSolution(Random random)
    {
        var grid = new int[CellCount];
        Search(grid, SolverState.From(grid)!.Value, limit: 1, random, fill: true);
        return grid;
    }

    /// <summary>Clears cells in random order, keeping each removal only if the solution stays unique.</summary>
    internal static int[] RemoveClues(int[] solution, int givens, Random random)
    {
        var puzzle = solution.ToArray();
        var remaining = CellCount;
        foreach (var index in Enumerable.Range(0, CellCount).OrderBy(_ => random.Next()))
        {
            if (remaining <= givens)
                break;
            var value = puzzle[index];
            puzzle[index] = 0;
            if (CountSolutions(puzzle) == 1)
                remaining--;
            else
                puzzle[index] = value;
        }
        return puzzle;
    }

    /// <summary>
    /// Backtracking over the empty cell with the fewest candidates. Counts solutions up to
    /// <paramref name="limit"/>; with <paramref name="fill"/>, leaves the first solution in <paramref name="grid"/>.
    /// </summary>
    private static int Search(int[] grid, SolverState state, int limit, Random? random, bool fill = false)
    {
        var best = -1;
        var bestCandidates = 0;
        var bestCount = 10;
        for (var i = 0; i < CellCount; i++)
        {
            if (grid[i] != 0)
                continue;
            var candidates = state.Candidates(i);
            var count = BitOperations.PopCount((uint)candidates);
            if (count < bestCount)
            {
                (best, bestCandidates, bestCount) = (i, candidates, count);
                if (count <= 1)
                    break;
            }
        }

        if (best < 0)
            return 1; // no empty cells: solved
        if (bestCount == 0)
            return 0;

        var digits = Enumerable.Range(1, Size).Where(d => (bestCandidates & (1 << d)) != 0);
        if (random is not null)
            digits = digits.OrderBy(_ => random.Next());

        var found = 0;
        foreach (var digit in digits)
        {
            grid[best] = digit;
            state.Place(best, digit);
            found += Search(grid, state, limit - found, random, fill);
            if (found >= limit && fill)
                return found; // keep the filled grid
            state.Remove(best, digit);
            grid[best] = 0;
            if (found >= limit)
                break;
        }
        return found;
    }

    private static int[] Parse(string digits) =>
        digits.Where(ch => char.IsDigit(ch) || ch == '.').Select(ch => ch == '.' ? 0 : ch - '0').ToArray() is { Length: CellCount } cells
            ? cells
            : throw new ArgumentException("A sudoku needs 81 cells", nameof(digits));

    /// <summary>Digits used per row, column and box, as bitmasks.</summary>
    private struct SolverState
    {
        private int[] _rows, _cols, _boxes;

        public static SolverState? From(int[] grid)
        {
            var state = new SolverState { _rows = new int[Size], _cols = new int[Size], _boxes = new int[Size] };
            for (var i = 0; i < CellCount; i++)
            {
                if (grid[i] == 0)
                    continue;
                if ((state.Candidates(i) & (1 << grid[i])) == 0)
                    return null; // the givens already conflict
                state.Place(i, grid[i]);
            }
            return state;
        }

        public readonly int Candidates(int index) =>
            AllDigits & ~(_rows[index / Size] | _cols[index % Size] | _boxes[Box(index)]);

        public readonly void Place(int index, int digit)
        {
            var bit = 1 << digit;
            _rows[index / Size] |= bit;
            _cols[index % Size] |= bit;
            _boxes[Box(index)] |= bit;
        }

        public readonly void Remove(int index, int digit)
        {
            var bit = ~(1 << digit);
            _rows[index / Size] &= bit;
            _cols[index % Size] &= bit;
            _boxes[Box(index)] &= bit;
        }

        private static int Box(int index) => index / Size / 3 * 3 + index % Size / 3;
    }
}
