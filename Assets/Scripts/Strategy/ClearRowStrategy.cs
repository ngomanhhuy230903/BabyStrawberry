using System.Collections.Generic;
using UnityEngine;

public class ClearRowStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        var newlyAffected = new List<Candy>();

        Debug.Log($"Executing ClearRowStrategy for candy at row {activatingCandy.yIndex}");

        for (int x = 0; x < board.boardWidth; x++)
        {
            // Kiểm tra node và candy tại vị trí trong hàng
            if (board.candyBoard[x, activatingCandy.yIndex]?.isUsable == true && board.candyBoard[x, activatingCandy.yIndex]?.candy != null)
            {
                Candy affectedCandy = board.candyBoard[x, activatingCandy.yIndex].candy.GetComponent<Candy>();

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