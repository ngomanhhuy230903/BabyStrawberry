// NoPossibleMovesState.cs
using UnityEngine;

public class NoPossibleMovesState : IBoardState
{
    private CandyBoard _board;

    public NoPossibleMovesState(CandyBoard board)
    {
        _board = board;
    }

    public void OnEnter()
    {
        Debug.Log("Entering NoPossibleMovesState");
        // Hiển thị thông báo, có thể gọi GameManager để xử lý UI
        // GameManager.instance.ShowNoMoreMovesPopup();
        // Tạm thời, sẽ thử khởi tạo lại board sau 1 giây
        _board.StartCoroutine(_board.HandleNoPossibleMovesCoroutine());
    }

    public void HandleCandyClick(Candy candy)
    {
        Debug.Log("No possible moves. Candy click ignored.");
    }

    public void OnExit()
    {
        Debug.Log("Exiting NoPossibleMovesState");
    }

    public void UpdateState() { }
}