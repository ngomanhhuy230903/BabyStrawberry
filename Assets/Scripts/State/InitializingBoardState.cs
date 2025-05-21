// InitializingBoardState.cs
using UnityEngine;
using System.Collections; // Cần cho IEnumerator

public class InitializingBoardState : IBoardState
{
    private readonly CandyBoard _board;
    private Coroutine _initializationProcessCoroutine; // Đổi tên để rõ ràng hơn

    public InitializingBoardState(CandyBoard board)
    {
        _board = board;
        if (_board == null)
        {
            Debug.LogError("InitializingBoardState: CandyBoard instance is null in constructor!");
        }
    }

    public void OnEnter()
    {
        if (_board == null)
        {
            Debug.LogError("InitializingBoardState: Cannot OnEnter because CandyBoard is null.");
            return;
        }

        Debug.Log("===== Entering InitializingBoardState =====");
        _board.isProcessingMove = true; // Đánh dấu board đang bận, ngăn các tương tác khác

        if (_board.gameObject.activeInHierarchy)
        {
            _initializationProcessCoroutine = _board.StartCoroutine(FullInitializationSequence());
        }
        else
        {
            Debug.LogWarning("InitializingBoardState: CandyBoard GameObject is not active. Cannot start initialization sequence.");
            // Nếu board không active, không thể làm gì nhiều, có thể chuyển về Idle hoặc một state lỗi.
            _board.isProcessingMove = false; // Reset cờ
            // _board.SetState(new IdleState(_board)); // Hoặc một state an toàn khác
        }
    }

    private IEnumerator FullInitializationSequence()
    {
        // 1. Dọn dẹp board cũ
        Debug.Log("InitializingBoardState: Starting board cleanup phase.");
        // Giả sử bạn có hàm này trong CandyBoard và nó xử lý việc trả kẹo về pool
        _board.ClearBoardForReinitialization();
        // 2. Khởi tạo board mới
        Debug.Log("InitializingBoardState: Starting new board generation phase.");
        yield return _board.StartCoroutine(_board.InitializeBoardCoroutineInternal());

        Debug.Log("InitializingBoardState: FullInitializationSequence completed.");

    }

    public void OnExit()
    {
        Debug.Log("===== Exiting InitializingBoardState =====");
        // Dừng coroutine bao bọc nếu nó vẫn đang chạy khi state thay đổi đột ngột
        // (mặc dù lý tưởng là nó nên tự hoàn thành và chuyển state).
        if (_initializationProcessCoroutine != null && _board != null && _board.gameObject.activeInHierarchy)
        {
            _board.StopCoroutine(_initializationProcessCoroutine);
            _initializationProcessCoroutine = null;
        }
    }

    public void HandleCandyClick(Candy candy)
    {
        // Không cho phép click khi đang khởi tạo
        // Debug.Log("InitializingBoardState: Board is initializing. Candy click ignored.");
    }

    public void UpdateState()
    {
        // Thường không cần logic update phức tạp ở đây,
        // trừ khi bạn muốn hiển thị hiệu ứng loading hoặc kiểm tra timeout.
    }
}