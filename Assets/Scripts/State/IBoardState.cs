// IBoardState.cs
public interface IBoardState
{
    void OnEnter(); // Khi vào trạng thái này
    void OnExit();  // Khi thoát khỏi trạng thái này
    void UpdateState(); // Được gọi mỗi frame
    void HandleCandyClick(Candy candy); // Xử lý khi một kẹo được click
    // Có thể thêm các phương thức khác cho các sự kiện cụ thể
    // ví dụ: void OnMoveProcessingComplete(bool foundMatch);
    // void OnBoardInitializationComplete(bool possibleMovesExist);
}