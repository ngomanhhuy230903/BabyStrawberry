// IdleState.cs
using UnityEngine;

public class IdleState : IBoardState
{
    private CandyBoard _board;

    public IdleState(CandyBoard board)
    {
        _board = board;
    }

    public void OnEnter()
    {
        Debug.Log("Entering IdleState");
        _board.DeselectCurrentCandy(); // Đảm bảo không có kẹo nào được chọn
        // Đảm bảo các cờ isProcessingMove (nếu còn) được reset, nhưng state đã thay thế nó
    }

    public void OnExit()
    {
        Debug.Log("Exiting IdleState");
    }

    public void HandleCandyClick(Candy candy)
    {
        if (candy == null || candy.isMoving)
        {
            Debug.LogWarning("IdleState: Clicked on null or moving candy.");
            return;
        }
        _board.SetSelectedCandy(candy); // Lưu kẹo được chọn
        candy.SetSelected(true);
        _board.SetState(new CandySelectedState(_board, candy)); // Chuyển sang trạng thái đã chọn kẹo
    }

    public void UpdateState()
    {
        // Trạng thái chờ, không làm gì đặc biệt trong Update
    }
}