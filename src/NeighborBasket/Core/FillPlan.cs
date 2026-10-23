using System;
using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>What the neighbor wants this round, as the basket screen shows it.</summary>
public sealed record BasketWish(bool IsActive, bool IsPending, bool IsAway, bool IsDying, IReadOnlyList<int> Categories, int? CrisisCategory)
{
    public static readonly BasketWish None = new(false, false, false, false, new int[0], null);
}

/// <summary>
/// One stack the player owns at home or in the bag. Value is what the whole stack counts for when she scores the
/// basket (trade value times uses times count, fuel already scaled); Weight is the whole stack's weight in the game's
/// units; Width and Height are the cells it takes in a bag.
/// </summary>
public sealed record Candidate(long ItemId, long OwnerId, int ConfigId, int Category, int Value, int Count, bool IsForbidden, int Weight, int Width, int Height)
{
    /// <summary>Forbidden items are refused by the basket, and items with no trade value score nothing, so neither is offered.</summary>
    public bool IsUsable => !IsForbidden && Value > 0 && Count > 0 && Width > 0 && Height > 0;

    public int Area => Width * Height;
}

/// <summary>A cell rectangle in a bag: column, row, width, height.</summary>
public readonly record struct Cells(int X, int Y, int Width, int Height);

/// <summary>A stack to put in the basket, with the top-left cell it goes to.</summary>
public sealed record Pick(Candidate Item, int X, int Y)
{
    public int Weight => Item.Weight;
}

/// <summary>The basket as it stands: its grid, the cells its items already take, the weight left, and the categories it holds.</summary>
public sealed record Basket(int Cols, int Rows, IReadOnlyList<Cells> Taken, float FreeWeight, IReadOnlyCollection<int> CategoriesInBasket);

/// <summary>The game's send scoring: a flat amount per wished category she gets, and value at one rate for wished items and another for the rest.</summary>
public sealed record Rates(int HitPerCategory, float HitRate, float MissRate)
{
    public static readonly Rates Game = new(15, 0.2f, 0.06f);
}

public enum FillStop
{
    None,
    NoNeighbor,
    Away,
    Pending,
    Full,
}

/// <summary>
/// The picks, the wished categories nothing at hand could cover, why nothing was picked when nothing was, and the
/// progress the picks add by the game's formula (the game rounds the whole basket, so it can differ by one).
/// </summary>
public sealed record FillResult(IReadOnlyList<Pick> Picks, IReadOnlyList<int> Missing, FillStop Stop, bool IsFoodOnly, int Gain = 0);

/// <summary>
/// Packs the basket as tightly as it goes for the most progress. Each wished category she gets earns a flat bonus and
/// every item adds its value at the wished or the other rate, so the plan is the set of whole stacks, placed on the
/// basket's grid by their shapes and within its weight, that scores highest. While she is dying only food counts, so
/// only food goes in, the most value first.
/// </summary>
public static class FillPlan
{
    public const int FoodCategory = 1;

    /// <summary>The search stops after this many steps and keeps the best plan found; a 5 x 3 basket rarely needs a tenth of it.</summary>
    public const int SearchBudget = 400_000;

    public static FillResult Plan(BasketWish wish, IReadOnlyList<Candidate> sources, Basket basket, Rates rates)
    {
        if (!wish.IsActive) return Stopped(FillStop.NoNeighbor);
        if (wish.IsAway) return Stopped(FillStop.Away);

        var wished = wish.IsDying ? new List<int>() : Wanted(wish).ToList();

        if (!wish.IsDying && wished.Count == 0) return Stopped(FillStop.Pending);

        var grid = new Grid(basket.Cols, basket.Rows, basket.Taken);

        if (grid.FreeCount == 0 || basket.FreeWeight <= 0) return Stopped(FillStop.Full);

        var usable = sources.Where(item => item.IsUsable && (!wish.IsDying || item.Category == FoodCategory)).ToList();
        var missing = wish.IsDying
            ? (usable.Count == 0 ? new List<int> { FoodCategory } : new List<int>())
            : wished.Where(category => !basket.CategoriesInBasket.Contains(category) && usable.All(item => item.Category != category)).ToList();
        var scorer = new Scorer(wished, basket.CategoriesInBasket, rates, wish.IsDying);
        var picks = new Search(grid, Shortlist(usable, scorer, grid), scorer, basket.FreeWeight).Run();
        var gain = (int)Math.Round(scorer.Score(picks.Select(pick => pick.Item)));

        if (picks.Count == 0 && missing.Count == 0) return Stopped(FillStop.Full);

        return new FillResult(picks, missing, FillStop.None, wish.IsDying, gain);
    }

    /// <summary>
    /// The progress a set of stacks adds by the game's formula: the flat bonus for each wished category not already in
    /// the basket, plus each stack's value at the wished or the other rate. While she is dying, food value alone.
    /// </summary>
    public static double Score(IEnumerable<Candidate> items, BasketWish wish, IReadOnlyCollection<int> categoriesInBasket, Rates rates) =>
        new Scorer(wish.IsDying ? new List<int>() : Wanted(wish).ToList(), categoriesInBasket, rates, wish.IsDying).Score(items);

    /// <summary>The crisis category first, then her wishes in the order she named them, each once.</summary>
    private static IEnumerable<int> Wanted(BasketWish wish)
    {
        var seen = new HashSet<int>();

        if (wish.CrisisCategory is { } crisis && seen.Add(crisis)) yield return crisis;

        if (wish.IsPending) yield break;

        foreach (var category in wish.Categories)
        {
            if (seen.Add(category)) yield return category;
        }
    }

    /// <summary>
    /// The stacks worth searching. For each shape: as many of the best as could ever fit, the same by value for their
    /// weight (for when weight runs short), and the best two of each wished category (for the flat bonus). Anything else
    /// is beaten by a kept stack of the same shape, so dropping it loses nothing but search time.
    /// </summary>
    private static List<Candidate> Shortlist(List<Candidate> usable, Scorer scorer, Grid grid)
    {
        var kept = new Dictionary<long, Candidate>();

        foreach (var shape in usable.GroupBy(item => (item.Width, item.Height)))
        {
            var room = grid.FreeCount / (shape.Key.Width * shape.Key.Height);

            if (room == 0) continue;

            var byValue = shape.OrderByDescending(scorer.Rated).ThenBy(item => item.Weight).ThenBy(item => item.ItemId);
            var byWeight = shape.OrderByDescending(item => scorer.Rated(item) / Math.Max(1, item.Weight)).ThenBy(item => item.ItemId);

            foreach (var item in byValue.Take(room).Concat(byWeight.Take(room))) kept[item.ItemId] = item;

            foreach (var category in shape.Where(scorer.IsWished).GroupBy(item => item.Category))
            {
                foreach (var item in category.OrderByDescending(scorer.Rated).ThenBy(item => item.ItemId).Take(2)) kept[item.ItemId] = item;
            }
        }

        return kept.Values.OrderByDescending(item => scorer.Rated(item) / item.Area).ThenBy(item => item.ItemId).ToList();
    }

    private static FillResult Stopped(FillStop stop) => new(new Pick[0], new int[0], stop, false);

    private sealed class Scorer
    {
        private readonly HashSet<int> _wished;
        private readonly IReadOnlyCollection<int> _inBasket;
        private readonly Rates _rates;
        private readonly bool _isDying;

        public Scorer(List<int> wished, IReadOnlyCollection<int> inBasket, Rates rates, bool isDying)
        {
            _wished = wished.ToHashSet();
            _inBasket = inBasket;
            _rates = rates;
            _isDying = isDying;
        }

        public int HitPerCategory => _isDying ? 0 : _rates.HitPerCategory;

        public bool IsWished(Candidate item) => _wished.Contains(item.Category);

        /// <summary>Whether this stack would earn the flat bonus, its category being wished and not yet in the basket.</summary>
        public bool CanHit(Candidate item) => !_isDying && IsWished(item) && !_inBasket.Contains(item.Category);

        /// <summary>A stack's value at its rate, without the flat bonus.</summary>
        public double Rated(Candidate item) => _isDying ? item.Value : item.Value * (double)(IsWished(item) ? _rates.HitRate : _rates.MissRate);

        public double Score(IEnumerable<Candidate> items)
        {
            var hits = new HashSet<int>();
            var total = 0.0;

            foreach (var item in items)
            {
                total += Rated(item);

                if (CanHit(item)) hits.Add(item.Category);
            }

            return total + hits.Count * HitPerCategory;
        }
    }

    /// <summary>The basket's cells as bits, row by row; a set bit is taken.</summary>
    private sealed class Grid
    {
        public Grid(int cols, int rows, IReadOnlyList<Cells> taken)
        {
            Cols = Math.Max(0, cols);
            Rows = Math.Max(0, rows);

            if (Cols * Rows > 62) throw new ArgumentException($"a {Cols} x {Rows} bag is too big to plan");

            foreach (var cells in taken) Taken |= Mask(cells.X, cells.Y, cells.Width, cells.Height, clip: true);

            FreeCount = Cols * Rows - BitCount(Taken);
        }

        public int Cols { get; }

        public int Rows { get; }

        public long Taken { get; }

        public int FreeCount { get; }

        public long Full => (1L << (Cols * Rows)) - 1;

        /// <summary>The bits of a rectangle, or 0 when it leaves the grid (clip keeps the part inside instead).</summary>
        public long Mask(int x, int y, int width, int height, bool clip = false)
        {
            if (!clip && (x < 0 || y < 0 || x + width > Cols || y + height > Rows)) return 0;

            var mask = 0L;

            for (var row = Math.Max(0, y); row < Math.Min(Rows, y + height); row++)
            {
                for (var col = Math.Max(0, x); col < Math.Min(Cols, x + width); col++) mask |= 1L << (row * Cols + col);
            }

            return mask;
        }

        public static int BitCount(long bits)
        {
            var count = 0;

            for (; bits != 0; bits &= bits - 1) count++;

            return count;
        }
    }

    /// <summary>
    /// Depth first over the first open cell: put a stack there by its top-left corner, or leave the cell empty. A branch
    /// is cut when even filling every open cell at the best rate left, with every wished category still to get, could
    /// not beat the best plan so far. Stacks are tried best rate per cell first, so the first plans found are good ones.
    /// </summary>
    private sealed class Search
    {
        private readonly Grid _grid;
        private readonly List<Candidate> _items;
        private readonly Scorer _scorer;
        private readonly float _freeWeight;
        private readonly bool[] _used;
        private readonly List<Pick> _current = new();
        private List<Pick> _best = new();
        private double _bestScore;
        private int _steps;

        public Search(Grid grid, List<Candidate> items, Scorer scorer, float freeWeight)
        {
            _grid = grid;
            _items = items;
            _scorer = scorer;
            _freeWeight = freeWeight;
            _used = new bool[items.Count];
        }

        public List<Pick> Run()
        {
            Step(_grid.Taken, 0, 0.0, new HashSet<int>());

            return _best;
        }

        private void Step(long closed, float weight, double rated, HashSet<int> hits)
        {
            var score = rated + hits.Count * _scorer.HitPerCategory;

            if (score > _bestScore + 1e-9)
            {
                _bestScore = score;
                _best = _current.ToList();
            }

            if (closed == _grid.Full || ++_steps > SearchBudget) return;
            if (score + Bound(closed, hits) <= _bestScore + 1e-9) return;

            var cell = LowestOpen(closed);
            var x = cell % _grid.Cols;
            var y = cell / _grid.Cols;

            for (var i = 0; i < _items.Count; i++)
            {
                if (_used[i]) continue;

                var item = _items[i];
                var mask = _grid.Mask(x, y, item.Width, item.Height);

                if (mask == 0 || (mask & closed) != 0 || weight + item.Weight > _freeWeight) continue;

                var isNewHit = _scorer.CanHit(item) && hits.Add(item.Category);

                _used[i] = true;
                _current.Add(new Pick(item, x, y));
                Step(closed | mask, weight + item.Weight, rated + _scorer.Rated(item), hits);
                _current.RemoveAt(_current.Count - 1);
                _used[i] = false;

                if (isNewHit) hits.Remove(item.Category);
            }

            // Leave this cell empty and carry on with the rest.
            Step(closed | (1L << cell), weight, rated, hits);
        }

        /// <summary>The most the open cells could still add: each at the best rate per cell left, plus every bonus not yet won.</summary>
        private double Bound(long closed, HashSet<int> hits)
        {
            var open = Grid.BitCount(_grid.Full & ~closed);
            var bestRate = 0.0;
            var bonuses = new HashSet<int>();

            for (var i = 0; i < _items.Count; i++)
            {
                if (_used[i] || _items[i].Area > open) continue;

                bestRate = Math.Max(bestRate, _scorer.Rated(_items[i]) / _items[i].Area);

                if (_scorer.CanHit(_items[i]) && !hits.Contains(_items[i].Category)) bonuses.Add(_items[i].Category);
            }

            return open * bestRate + Math.Min(bonuses.Count, open) * _scorer.HitPerCategory;
        }

        private static int LowestOpen(long closed)
        {
            var cell = 0;

            while ((closed & (1L << cell)) != 0) cell++;

            return cell;
        }
    }
}
