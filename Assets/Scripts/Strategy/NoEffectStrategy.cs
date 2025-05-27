// NoEffectStrategy.cs
using System.Collections.Generic;
using UnityEngine; // Cần cho Debug

public class NoEffectStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        return new List<Candy>();
    }

}