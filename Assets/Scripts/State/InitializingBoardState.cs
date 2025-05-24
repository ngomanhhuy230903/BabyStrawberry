// InitializingBoardState.cs - Phiên bản đã sửa lỗi
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

        if (_board.gameObject.activeInHierarchy)
        {
            _initializationProcessCoroutine = _board.StartCoroutine(FullInitializationSequence());
        }
        else
        {
            Debug.LogWarning("InitializingBoardState: CandyBoard GameObject is not active. Cannot start initialization sequence.");
        }
    }

    private IEnumerator FullInitializationSequence()
    {
        // 0. KHỞI TẠO TRẠNG THÁI GAME -> ĐÂY LÀ BƯỚC SỬA LỖI
        // Gọi GameManager để thiết lập điểm, lượt đi, và phát sự kiện cập nhật UI lần đầu
        if (GameManager.instance != null)
        {
            Debug.Log("InitializingBoardState: Calling GameManager.Initialize() to set up game state and fire initial UI events.");
            GameManager.instance.Initialize();
        }
        else
        {
            Debug.LogError("InitializingBoardState: GameManager.instance is null. Cannot initialize game state.");
            yield break; // Dừng lại nếu không có GameManager
        }

        // 1. Dọn dẹp board cũ
        Debug.Log("InitializingBoardState: Starting board cleanup phase.");
        _board.ClearEntireBoard();

        // 2. Khởi tạo board mới
        Debug.Log("InitializingBoardState: Starting new board generation phase.");
        yield return _board.StartCoroutine(_board.InitializeBoardCoroutineInternal());

        Debug.Log("InitializingBoardState: FullInitializationSequence completed.");
    }

    public void OnExit()
    {
        Debug.Log("===== Exiting InitializingBoardState =====");
        if (_initializationProcessCoroutine != null && _board != null && _board.gameObject.activeInHierarchy)
        {
            _board.StopCoroutine(_initializationProcessCoroutine);
            _initializationProcessCoroutine = null;
        }
    }

    public void HandleCandyClick(Candy candy) { }
    public void UpdateState() { }
}