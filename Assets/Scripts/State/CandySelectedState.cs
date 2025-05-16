// CandySelectedState.cs
using UnityEngine;

public class CandySelectedState : IBoardState
{
    private CandyBoard _board;
    private Candy _firstSelectedCandy;

    public CandySelectedState(CandyBoard board, Candy firstSelectedCandy)
    {
        _board = board;
        _firstSelectedCandy = firstSelectedCandy;
    }

    public void OnEnter()
    {
        Debug.Log($"Entering CandySelectedState with {_firstSelectedCandy.name}");
    }

    public void OnExit()
    {
        Debug.Log("Exiting CandySelectedState");
        // _firstSelectedCandy.SetSelected(false); // Bỏ chọn khi thoát (trừ khi swap thành công)
        // Việc bỏ chọn nên được xử lý cẩn thận hơn, ví dụ trong DeselectCurrentCandy
    }

    public void HandleCandyClick(Candy secondCandy)
    {
        if (secondCandy == null || secondCandy.isMoving)
        {
            Debug.LogWarning("CandySelectedState: Clicked on null or moving candy.");
            return;
        }

        if (_firstSelectedCandy == secondCandy) // Click lại vào kẹo đã chọn
        {
            _firstSelectedCandy.SetSelected(false);
            _board.DeselectCurrentCandy();
            _board.SetState(new IdleState(_board));
        }
        else if (_board.IsAdjacent(_firstSelectedCandy, secondCandy)) // Kẹo thứ hai hợp lệ để swap
        {
            // Bỏ chọn kẹo đầu tiên về mặt hình ảnh trước khi swap
            // _firstSelectedCandy.SetSelected(false);
            // _board.DeselectCurrentCandy(); // selectedCandy sẽ được clear trong ProcessingMoveState hoặc khi IdleState bắt đầu

            _board.SetState(new ProcessingMoveState(_board, _firstSelectedCandy, secondCandy));
        }
        else // Chọn một kẹo khác không liền kề
        {
            _firstSelectedCandy.SetSelected(false); // Bỏ chọn kẹo cũ
            secondCandy.SetSelected(true);    // Chọn kẹo mới
            _board.SetSelectedCandy(secondCandy); // Cập nhật selectedCandy trong CandyBoard
            _board.SetState(new CandySelectedState(_board, secondCandy)); // Vẫn ở CandySelectedState nhưng với kẹo mới
        }
    }

    public void UpdateState() { }
}