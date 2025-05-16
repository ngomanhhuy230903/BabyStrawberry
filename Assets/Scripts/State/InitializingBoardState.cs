// InitializingBoardState.cs
using UnityEngine;

public class InitializingBoardState : IBoardState
{
    private CandyBoard _board;
    private Coroutine _initializationCoroutine;

    public InitializingBoardState(CandyBoard board)
    {
        _board = board;
    }

    public void OnEnter()
    {
        Debug.Log("Entering InitializingBoardState");
        _initializationCoroutine = _board.StartCoroutine(_board.InitializeBoardCoroutineInternal());
    }

    public void OnExit()
    {
        Debug.Log("Exiting InitializingBoardState");
        if (_initializationCoroutine != null)
        {
            // Không cần thiết phải stop coroutine ở đây nếu nó tự kết thúc và chuyển state
            // _board.StopCoroutine(_initializationCoroutine);
        }
    }

    public void HandleCandyClick(Candy candy)
    {
        // Không cho phép click khi đang khởi tạo
        Debug.Log("Board is initializing. Candy click ignored.");
    }

    public void UpdateState()
    {
        // Có thể thêm logic update nếu cần trong khi khởi tạo (ví dụ: hiển thị loading)
    }
}