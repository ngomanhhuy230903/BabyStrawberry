using System.Collections.Generic;
using UnityEngine;

public class ClearColumnStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        var newlyAffected = new List<Candy>();

        Debug.Log($"Executing ClearColumnStrategy for candy at column {activatingCandy.xIndex}");

        for (int y = 0; y < board.boardHeight; y++)
        {
            // Kiểm tra node và candy tại vị trí trong cột
            if (board.candyBoard[activatingCandy.xIndex, y]?.isUsable == true && board.candyBoard[activatingCandy.xIndex, y]?.candy != null)
            {
                Candy affectedCandy = board.candyBoard[activatingCandy.xIndex, y].candy.GetComponent<Candy>();

                // Chỉ thêm vào danh sách nếu nó chưa được xử lý
                if (affectedCandy != null && !allCandiesToDestroySet.Contains(affectedCandy))
                {
                    newlyAffected.Add(affectedCandy);
                }
            }
        }
        return newlyAffected;
    }
}