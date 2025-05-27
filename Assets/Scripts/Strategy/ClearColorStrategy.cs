using System.Collections.Generic;
using UnityEngine;

public class ClearColorStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        var newlyAffected = new List<Candy>();
        if (otherCandy == null || otherCandy.isSpecial) return newlyAffected; // Chỉ kích hoạt với kẹo thường

        CandyType targetType = otherCandy.candyType;

        for (int x = 0; x < board.boardWidth; x++)
        {
            for (int y = 0; y < board.boardHeight; y++)
            {
                if (board.candyBoard[x, y]?.isUsable == true && board.candyBoard[x, y]?.candy != null)
                {
                    Candy c = board.candyBoard[x, y].candy.GetComponent<Candy>();
                    if (c != null && !c.isSpecial && c.candyType == targetType)
                    {
                        if (!allCandiesToDestroySet.Contains(c))
                        {
                            newlyAffected.Add(c);
                        }
                    }
                }
            }
        }
        return newlyAffected;
    }
}