// NoEffectStrategy.cs
using System.Collections.Generic;
using UnityEngine; // Cần cho Debug

public class NoEffectStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy specialCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        // Không làm gì cả
        Debug.Log($"Executing NoEffectStrategy for candy at [{specialCandy.xIndex},{specialCandy.yIndex}]");
        return new List<Candy>();
    }
}