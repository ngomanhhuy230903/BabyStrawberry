// ClearColumnStrategy.cs
using System.Collections.Generic;
using UnityEngine; // Cần cho Debug

public class ClearColumnStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy specialCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        List<Candy> newlyAffectedBySpecial = new List<Candy>();
        Debug.Log($"Executing ClearColumnStrategy for candy at [{specialCandy.xIndex},{specialCandy.yIndex}] in column {specialCandy.xIndex}");

        for (int y = 0; y < board.boardHeight; y++)
        {
            if (board.candyBoard[specialCandy.xIndex, y].isUsable && board.candyBoard[specialCandy.xIndex, y].candy != null)
            {
                Candy affectedCandy = board.candyBoard[specialCandy.xIndex, y].candy.GetComponent<Candy>();
                if (affectedCandy != null)
                {
                    if (allCandiesToDestroySet.Add(affectedCandy))
                    {
                        newlyAffectedBySpecial.Add(affectedCandy);
                        Debug.Log($"ClearColumnStrategy added {affectedCandy.name} to destruction set.");
                    }
                }
            }
        }
        return newlyAffectedBySpecial;
    }
}