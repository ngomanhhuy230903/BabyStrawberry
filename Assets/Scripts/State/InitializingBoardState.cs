using UnityEngine;
using System.Collections;

public class InitializingBoardState : IBoardState
{
    private readonly CandyBoard _board;
    private Coroutine _initializationProcessCoroutine;

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
        // BỎ: _board.isProcessingMove = true; -> Trạng thái này tự nó đã là một "cờ" báo bận.

        if (_board.gameObject.activeInHierarchy)
        {
            _initializationProcessCoroutine = _board.StartCoroutine(FullInitializationSequence());
        }
        else
        {
            Debug.LogWarning("InitializingBoardState: CandyBoard GameObject is not active. Cannot start initialization sequence.");
            // BỎ: _board.isProcessingMove = false;
        }
    }

    private IEnumerator FullInitializationSequence()
    {
        // 1. Dọn dẹp board cũ
        Debug.Log("InitializingBoardState: Starting board cleanup phase.");
        // THAY ĐỔI: Sử dụng phương thức dọn dẹp còn lại trong CandyBoard
        _board.ClearEntireBoard();

        // 2. Khởi tạo board mới
        Debug.Log("InitializingBoardState: Starting new board generation phase.");
        yield return _board.StartCoroutine(_board.InitializeBoardCoroutineInternal());

        Debug.Log("InitializingBoardState: FullInitializationSequence completed.");
    }

    public void OnExit()
    {
        Debug.Log("===== Exiting InitializingBoardState =====");
        // Dừng coroutine nếu nó vẫn đang chạy khi state thay đổi đột ngột.
        if (_initializationProcessCoroutine != null && _board != null && _board.gameObject.activeInHierarchy)
        {
            _board.StopCoroutine(_initializationProcessCoroutine);
            _initializationProcessCoroutine = null;
        }
    }

    public void HandleCandyClick(Candy candy)
    {
        // Không cho phép click khi đang khởi tạo. Logic này đúng và không cần thay đổi.
    }

    public void UpdateState()
    {
        // Không cần logic update ở đây.
    }
}