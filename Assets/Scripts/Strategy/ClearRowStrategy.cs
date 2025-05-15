// ClearRowStrategy.cs
using System.Collections.Generic;
using UnityEngine; // Cần cho Debug

public class ClearRowStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy specialCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        List<Candy> newlyAffectedBySpecial = new List<Candy>();
        Debug.Log($"Executing ClearRowStrategy for candy at [{specialCandy.xIndex},{specialCandy.yIndex}] in row {specialCandy.yIndex}");

        for (int x = 0; x < board.boardWidth; x++)
        {
            if (board.candyBoard[x, specialCandy.yIndex].isUsable && board.candyBoard[x, specialCandy.yIndex].candy != null)
            {
                Candy affectedCandy = board.candyBoard[x, specialCandy.yIndex].candy.GetComponent<Candy>();
                if (affectedCandy != null)
                {
                    // .Add của HashSet trả về true nếu item được thêm mới
                    if (allCandiesToDestroySet.Add(affectedCandy))
                    {
                        newlyAffectedBySpecial.Add(affectedCandy);
                        Debug.Log($"ClearRowStrategy added {affectedCandy.name} to destruction set.");
                    }
                }
            }
        }
        return newlyAffectedBySpecial;
    }
}