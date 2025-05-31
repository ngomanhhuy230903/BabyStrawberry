// IdleState.cs
using UnityEngine;

public class IdleState : IBoardState
{
    private CandyBoard _board;

    public IdleState(CandyBoard board)
    {
        _board = board;
    }

    public void OnEnter() // THAY ĐỔI
    {
        Debug.Log("Entering IdleState. Hint timer starting.");
        _board.DeselectCurrentCandy();
        _board.ResetIdleTimerAndStopHints(); // Reset bộ đếm và dừng gợi ý cũ khi vào Idle
    }

    public void OnExit() // THAY ĐỔI
    {
        Debug.Log("Exiting IdleState.");
        _board.ResetIdleTimerAndStopHints(); // Dừng gợi ý khi thoát khỏi Idle
    }

    public void HandleCandyClick(Candy candy) // THAY ĐỔI
    {
        if (candy == null || candy.isMoving || (_board.gameManager != null && _board.gameManager.isGameOver))
        {
            Debug.LogWarning("IdleState: Clicked on null, moving candy, or game is over.");
            return;
        }

        _board.ResetIdleTimerAndStopHints(); // Người chơi đã tương tác, reset!

        _board.SetSelectedCandy(candy);
        candy.SetSelected(true);
        _board.SetState(new CandySelectedState(_board, candy));
    }

    public void UpdateState() // THAY ĐỔI HOÀN TOÀN
    {
        if (_board.gameManager != null && _board.gameManager.isGameOver) return;

        _board._idleTimer += Time.deltaTime;

        if (_board._hintAnimationCoroutine == null && _board._idleTimer >= _board.hintDelay)
        {
            Debug.Log("Idle time limit reached. Attempting to show hint.");
            _board._listOfPossibleMoves = _board.FindAllPossibleMoves();

            if (_board._listOfPossibleMoves.Count > 0)
            {
                _board._hintAnimationCoroutine = _board.StartCoroutine(_board.AnimateHintCandiesCoroutine());
            }
            else
            {
                Debug.Log("No possible moves to hint. Board might be in a no-moves state.");
            }
            _board._idleTimer = 0f;
        }
    }
}