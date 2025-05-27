using System.Collections.Generic;
using UnityEngine;

public class ClearBoardStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        var newlyAffected = new List<Candy>();
        for (int x = 0; x < board.boardWidth; x++)
        {
            for (int y = 0; y < board.boardHeight; y++)
            {
                if (board.candyBoard[x, y]?.isUsable == true && board.candyBoard[x, y]?.candy != null)
                {
                    Candy c = board.candyBoard[x, y].candy.GetComponent<Candy>();
                    if (c != null && c.gameObject.activeSelf && !allCandiesToDestroySet.Contains(c))
                    {
                        newlyAffected.Add(c);
                    }
                }
            }
        }
        return newlyAffected;
    }
}