// ProcessingMoveState.cs
using UnityEngine;

public class ProcessingMoveState : IBoardState
{
    private CandyBoard _board;
    private Candy _candy1, _candy2; // Các kẹo tham gia vào lượt đi (có thể là swap hoặc kích hoạt special)
    private Coroutine _processingCoroutine;

    // Constructor cho lượt đi từ swap
    public ProcessingMoveState(CandyBoard board, Candy candy1, Candy candy2)
    {
        _board = board;
        _candy1 = candy1;
        _candy2 = candy2;
    }

    // Constructor cho lượt đi từ kích hoạt kẹo đặc biệt (nếu có, hoặc khi cascade)
    // public ProcessingMoveState(CandyBoard board)
    // {
    //     _board = board;
    //     // Trong trường hợp này, ProcessTurnOnMatchedBoard sẽ được gọi trực tiếp
    // }

    public void OnEnter()
    {
        Debug.Log("Entering ProcessingMoveState");
        _board.DeselectCurrentCandy(); // Xóa lựa chọn hiện tại vì đã bắt đầu xử lý

        if (_candy1 != null && _candy2 != null) // Lượt đi từ swap
        {
            _processingCoroutine = _board.StartCoroutine(_board.ProcessSwapAndMatchesCoroutine(_candy1, _candy2));
        }
        // else // Lượt đi từ cascade hoặc khởi tạo board có match sẵn
        // {
        //    // CheckBoard() sẽ được gọi và nếu có match, ProcessTurnOnMatchedBoard sẽ chạy
        //    // Điều này được quản lý trong InitializeBoardCoroutineInternal hoặc cuối ProcessSwapAndMatchesCoroutine
        //    // có lẽ cần một cách khác để kích hoạt nó nếu không phải từ swap
        //    // Hiện tại, giả sử ProcessTurnOnMatchedBoard được gọi từ các coroutine khác
        // }
    }

    public void OnExit()
    {
        Debug.Log("Exiting ProcessingMoveState");
    }

    public void HandleCandyClick(Candy candy)
    {
        Debug.Log("Board is processing a move. Candy click ignored.");
    }

    public void UpdateState() { }
}