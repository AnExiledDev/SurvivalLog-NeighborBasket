using System;

namespace NeighborBasket.Core;

/// <summary>One unit move: whether a unit went, and the id of the stack left where it was (null when none is left or it cannot be found).</summary>
public readonly record struct UnitStep(bool Moved, long? Rest);

public static class UnitSteps
{
    /// <summary>
    /// Moves up to <paramref name="count"/> units of a stack, one at a time, and returns how many went. Stacking's
    /// one-unit move sends the stack's own item away as the unit and leaves the rest as a new stack under a new id, so
    /// each step carries on from the id the last one left behind.
    /// </summary>
    public static int Move(long itemId, int count, Func<long, UnitStep> moveOne)
    {
        var moved = 0;
        long? current = itemId;

        while (moved < count && current is { } id)
        {
            var step = moveOne(id);

            if (!step.Moved) break;

            moved++;
            current = step.Rest;
        }

        return moved;
    }
}
