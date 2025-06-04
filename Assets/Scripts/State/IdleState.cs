// IdleState.cs
using UnityEngine;

public class IdleState : IBoardState
{
    private CandyBoard _board;
    // Không cần _idleTimer hay _hintCoroutine ở đây nữa, CandyBoard sẽ quản lý

    public IdleState(CandyBoard board)
    {
        _board = board;
    }

    public void OnEnter()
    {
        Debug.Log("Entering IdleState");
        _board.DeselectCurrentCandy();
        _board.StartIdleTimer(); // Bắt đầu timer gợi ý khi vào IdleState
    }

    public void OnExit()
    {
        Debug.Log("Exiting IdleState");
        _board.ResetIdleTimer(); // Dừng và reset timer gợi ý khi thoát IdleState
                                 // ResetIdleTimer cũng sẽ gọi StopHint()
    }

    public void HandleCandyClick(Candy candy)
    {
        if (candy == null || candy.isMoving)
        {
            Debug.LogWarning("IdleState: Clicked on null or moving candy.");
            return;
        }

        // Khi người chơi click, dừng timer và mọi gợi ý đang hiển thị
        _board.ResetIdleTimer();

        _board.SetSelectedCandy(candy);
        candy.SetSelected(true);
        // Khi chuyển sang CandySelectedState, OnExit của IdleState sẽ được gọi,
        // và sau đó SetState sẽ quản lý việc bắt đầu lại timer nếu cần.
        _board.SetState(new CandySelectedState(_board, candy));
    }

    public void UpdateState()
    {
        // Logic timer gợi ý giờ đã được quản lý bởi Coroutine trong CandyBoard,
        // không cần cập nhật thủ công ở đây nữa.
    }
}