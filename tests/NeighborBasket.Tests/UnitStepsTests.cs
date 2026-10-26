using System.Collections.Generic;
using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class UnitStepsTests
{
    /// <summary>
    /// A stack the way Stacking's one-unit move treats it: the stack's own item goes into the basket as the one unit,
    /// and what is left stays behind as a new stack with a new id.
    /// </summary>
    private sealed class FakeStack
    {
        public long Id = 100;
        public int Count;
        public readonly List<long> Asked = new();

        public FakeStack(int count) => Count = count;

        public UnitStep MoveOne(long id)
        {
            Asked.Add(id);

            if (id != Id || Count <= 1) return new UnitStep(false, null);

            Count--;
            Id++;

            return new UnitStep(true, Id);
        }
    }

    [Fact]
    public void Each_unit_after_the_first_is_taken_from_the_new_stack_left_behind()
    {
        var stack = new FakeStack(12);

        Assert.Equal(4, UnitSteps.Move(stack.Id, 4, stack.MoveOne));
        Assert.Equal(8, stack.Count);
        Assert.Equal(new long[] { 100, 101, 102, 103 }, stack.Asked);
    }

    [Fact]
    public void It_stops_when_a_move_is_refused_or_nothing_is_left_behind()
    {
        var refused = new FakeStack(3);

        Assert.Equal(2, UnitSteps.Move(refused.Id, 5, refused.MoveOne));
        Assert.Equal(1, refused.Count);

        var lost = 0;

        Assert.Equal(1, UnitSteps.Move(7, 5, id =>
        {
            lost++;

            return new UnitStep(true, null);
        }));
        Assert.Equal(1, lost);
    }
}
