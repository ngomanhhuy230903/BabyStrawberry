// ISpecialCandyEffectStrategy.cs
using System.Collections.Generic;
using UnityEngine; // Cần cho Debug

public interface ISpecialCandyEffectStrategy
{
    List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet);
}