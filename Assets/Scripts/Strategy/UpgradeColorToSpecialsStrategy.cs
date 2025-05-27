using System.Collections.Generic;
using UnityEngine;

public class UpgradeColorToSpecialsStrategy : ISpecialCandyEffectStrategy
{
    public List<Candy> Activate(CandyBoard board, Candy activatingCandy, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        var newlyAffected = new List<Candy>();
        if (otherCandy == null || !otherCandy.isSpecial || otherCandy.specialEffect == SpecialCandyEffect.ClearColor)
            return newlyAffected;

        CandyType targetType = otherCandy.candyType;
        SpecialCandyEffect effectToApply = otherCandy.specialEffect;

        // Thu thập và trả về pool các kẹo cùng màu
        for (int x = 0; x < board.boardWidth; x++)
        {
            for (int y = 0; y < board.boardHeight; y++)
            {
                Node node = board.candyBoard[x, y];
                if (node?.isUsable == true && node?.candy != null)
                {
                    Candy c = node.candy.GetComponent<Candy>();
                    if (c != null && !c.isSpecial && c.candyType == targetType)
                    {
                        // Vị trí để tạo kẹo mới
                        Vector3 position = c.transform.position;

                        // Trả kẹo cũ vào pool
                        board.GetCandyFactory().ReturnCandyToPool(c);

                        // Tạo kẹo đặc biệt mới tại vị trí đó
                        Candy newSpecialCandy = board.GetCandyFactory().CreateSpecialCandy(targetType, effectToApply, x, y, position);
                        node.candy = newSpecialCandy.gameObject;

                        // Thêm vào danh sách bị ảnh hưởng để kích hoạt ngay lập tức
                        if (!allCandiesToDestroySet.Contains(newSpecialCandy))
                        {
                            newlyAffected.Add(newSpecialCandy);
                        }
                    }
                }
            }
        }
        return newlyAffected;
    }
}