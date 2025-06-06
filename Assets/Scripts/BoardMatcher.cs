using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Chứa tất cả các thuật toán logic để tìm kiếm và xác thực các match.
/// Đây là một class C# thuần túy, không kế thừa từ MonoBehaviour,
/// giúp cho việc kiểm thử và tái sử dụng dễ dàng hơn.
/// </summary>
public class BoardMatcher
{
    private readonly int _width;
    private readonly int _height;

    public BoardMatcher(int boardWidth, int boardHeight)
    {
        _width = boardWidth;
        _height = boardHeight;
    }

    /// <summary>
    /// Tìm tất cả các match hợp lệ trên bảng và thêm chúng vào danh sách.
    /// </summary>
    /// <returns>Trả về true nếu tìm thấy ít nhất một match.</returns>
    public bool FindAllMatches(Node[,] board, List<Candy> candiesToRemove)
    {
        candiesToRemove.Clear();

        // Reset trạng thái isMatched của tất cả kẹo
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (board[x, y]?.candy != null)
                {
                    board[x, y].candy.GetComponent<Candy>().isMatched = false;
                }
            }
        }

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (board[x, y].isUsable && board[x, y].candy != null)
                {
                    Candy candy = board[x, y].candy.GetComponent<Candy>();
                    if (candy != null && !candy.isMatched)
                    {
                        MatchResult matchResult = IsConnected(candy, board);
                        if (matchResult.connectionCandys.Count >= 3)
                        {
                            MatchResult superMatchResult = SuperMatch(matchResult, board);
                            foreach (Candy c in superMatchResult.connectionCandys)
                            {
                                if (!candiesToRemove.Contains(c))
                                {
                                    candiesToRemove.Add(c);
                                    c.isMatched = true;
                                }
                            }
                        }
                    }
                }
            }
        }
        return candiesToRemove.Count > 0;
    }

    /// <summary>
    /// Tìm một cặp kẹo có thể hoán đổi để tạo thành match (dùng cho hệ thống Gợi ý).
    /// </summary>
    /// <returns>Trả về một List chứa 2 viên kẹo, hoặc null nếu không tìm thấy.</returns>
    public List<Candy> FindHint(Node[,] board)
    {
        // Duyệt theo chiều ngang
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width - 1; x++)
            {
                if (CanSwap(x, y, x + 1, y, board)) return new List<Candy> { board[x, y].candy.GetComponent<Candy>(), board[x + 1, y].candy.GetComponent<Candy>() };
            }
        }

        // Duyệt theo chiều dọc
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height - 1; y++)
            {
                if (CanSwap(x, y, x, y + 1, board)) return new List<Candy> { board[x, y].candy.GetComponent<Candy>(), board[x, y + 1].candy.GetComponent<Candy>() };
            }
        }

        return null; // Không tìm thấy nước đi nào
    }

    /// <summary>
    /// Kiểm tra xem trên bảng có còn nước đi hợp lệ nào không.
    /// </summary>
    public bool HasPossibleMoves(Node[,] board)
    {
        // Tận dụng lại logic của FindHint để kiểm tra
        return FindHint(board) != null;
    }

    /// <summary>
    /// Kiểm tra xem việc hoán đổi 2 viên kẹo có tạo ra match hay không.
    /// </summary>
    private bool CanSwap(int x1, int y1, int x2, int y2, Node[,] board)
    {
        if (!IsValidPosition(x1, y1) || !IsValidPosition(x2, y2) ||
            !board[x1, y1].isUsable || !board[x2, y2].isUsable ||
            board[x1, y1].candy == null || board[x2, y2].candy == null)
        {
            return false;
        }

        // Tạm thời hoán đổi trong logic
        (board[x1, y1].candy, board[x2, y2].candy) = (board[x2, y2].candy, board[x1, y1].candy);

        Candy c1 = board[x1, y1].candy.GetComponent<Candy>();
        Candy c2 = board[x2, y2].candy.GetComponent<Candy>();

        // Cập nhật tạm thời chỉ số để IsConnected hoạt động đúng
        (c1.xIndex, c1.yIndex, c2.xIndex, c2.yIndex) = (x1, y1, x2, y2);

        bool matchFound = IsConnected(c1, board).connectionCandys.Count >= 3 ||
                          IsConnected(c2, board).connectionCandys.Count >= 3;

        // Hoán đổi lại vị trí cũ
        (c1.xIndex, c1.yIndex, c2.xIndex, c2.yIndex) = (x2, y2, x1, y1);
        (board[x1, y1].candy, board[x2, y2].candy) = (board[x2, y2].candy, board[x1, y1].candy);

        return matchFound;
    }


    /// <summary>
    /// Kiểm tra các kết nối theo chiều ngang và dọc từ một viên kẹo.
    /// </summary>
    private MatchResult IsConnected(Candy candy, Node[,] board)
    {
        List<Candy> horizontalCandys = new List<Candy> { candy };
        CheckDirection(candy, Vector2Int.right, horizontalCandys, board);
        CheckDirection(candy, Vector2Int.left, horizontalCandys, board);

        List<Candy> verticalCandys = new List<Candy> { candy };
        CheckDirection(candy, Vector2Int.up, verticalCandys, board);
        CheckDirection(candy, Vector2Int.down, verticalCandys, board);

        if (horizontalCandys.Count >= 5 || verticalCandys.Count >= 5)
        {
            return new MatchResult() { connectionCandys = horizontalCandys.Count > verticalCandys.Count ? horizontalCandys : verticalCandys, direction = MatchDirection.Super };
        }
        if (horizontalCandys.Count >= 3)
        {
            return new MatchResult() { connectionCandys = horizontalCandys, direction = horizontalCandys.Count == 4 ? MatchDirection.LongHorizontal : MatchDirection.Horizontal };
        }
        if (verticalCandys.Count >= 3)
        {
            return new MatchResult() { connectionCandys = verticalCandys, direction = verticalCandys.Count == 4 ? MatchDirection.LongVertical : MatchDirection.Vertical };
        }

        return new MatchResult();
    }

    /// <summary>
    /// Mở rộng một match đã tìm thấy để kiểm tra các trường hợp L và T.
    /// </summary>
    private MatchResult SuperMatch(MatchResult matchResult, Node[,] board)
    {
        if (matchResult.direction == MatchDirection.Horizontal || matchResult.direction == MatchDirection.LongHorizontal)
        {
            foreach (Candy candy in matchResult.connectionCandys.ToList())
            {
                List<Candy> extraCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.up, extraCandys, board);
                CheckDirection(candy, Vector2Int.down, extraCandys, board);
                if (extraCandys.Count >= 2)
                {
                    matchResult.connectionCandys.AddRange(extraCandys);
                    matchResult.direction = MatchDirection.Super;
                }
            }
        }
        else if (matchResult.direction == MatchDirection.Vertical || matchResult.direction == MatchDirection.LongVertical)
        {
            foreach (Candy candy in matchResult.connectionCandys.ToList())
            {
                List<Candy> extraCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.left, extraCandys, board);
                CheckDirection(candy, Vector2Int.right, extraCandys, board);
                if (extraCandys.Count >= 2)
                {
                    matchResult.connectionCandys.AddRange(extraCandys);
                    matchResult.direction = MatchDirection.Super;
                }
            }
        }
        return matchResult;
    }

    /// <summary>
    /// Helper method: kiểm tra liên tục theo một hướng và thêm kẹo cùng loại vào danh sách.
    /// </summary>
    private void CheckDirection(Candy startCandy, Vector2Int direction, List<Candy> connectedCandys, Node[,] board)
    {
        int nextX = startCandy.xIndex + direction.x;
        int nextY = startCandy.yIndex + direction.y;

        while (IsValidPosition(nextX, nextY) && board[nextX, nextY].isUsable && board[nextX, nextY].candy != null)
        {
            Candy nextCandy = board[nextX, nextY].candy.GetComponent<Candy>();
            if (nextCandy != null && !nextCandy.isMatched && nextCandy.candyType == startCandy.candyType)
            {
                connectedCandys.Add(nextCandy);
                nextX += direction.x;
                nextY += direction.y;
            }
            else
            {
                break;
            }
        }
    }

    private bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < _width && y >= 0 && y < _height;
    }
}