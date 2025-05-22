// File: GameEvents.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ thống quản lý sự kiện trung tâm cho game.
/// Các module khác có thể đăng ký (Subscribe) hoặc hủy đăng ký (Unsubscribe)
/// và kích hoạt (Trigger) các sự kiện.
/// </summary>
public static class GameEvents
{
    // Sự kiện khi điểm số thay đổi
    public static event Action<int> OnScoreChanged;
    public static void TriggerScoreChanged(int newScore) => OnScoreChanged?.Invoke(newScore);

    // Sự kiện khi số lượt đi thay đổi
    public static event Action<int> OnMovesChanged;
    public static void TriggerMovesChanged(int newMoves) => OnMovesChanged?.Invoke(newMoves);

    // Sự kiện khi mục tiêu thay đổi (ít dùng sau khi khởi tạo, nhưng vẫn hữu ích)
    public static event Action<int> OnGoalChanged;
    public static void TriggerGoalChanged(int newGoal) => OnGoalChanged?.Invoke(newGoal);

    // Sự kiện khi trò chơi kết thúc
    public enum GameOutcome { Victory, Defeat }
    public static event Action<GameOutcome> OnGameOver;
    public static void TriggerGameOver(GameOutcome outcome) => OnGameOver?.Invoke(outcome);

    // Sự kiện khi bảng kẹo được khởi tạo/sẵn sàng
    public static event Action OnBoardInitialized;
    public static void TriggerBoardInitialized() => OnBoardInitialized?.Invoke();

    // Sự kiện khi kẹo được ghép và phá hủy
    // Truyền danh sách kẹo bị phá hủy để các observer khác có thể sử dụng thông tin này (ví dụ: SoundManager, VFXManager)
    public static event Action<List<Candy>> OnCandiesMatchedAndDestroyed;
    public static void TriggerCandiesMatchedAndDestroyed(List<Candy> destroyedCandies) => OnCandiesMatchedAndDestroyed?.Invoke(destroyedCandies);

    // Sự kiện khi bảng kẹo được lấp đầy lại sau khi phá hủy
    public static event Action OnBoardRefilled;
    public static void TriggerBoardRefilled() => OnBoardRefilled?.Invoke();

    // Sự kiện khi không còn nước đi hợp lệ trên bảng
    public static event Action OnNoPossibleMoves;
    public static void TriggerNoPossibleMoves() => OnNoPossibleMoves?.Invoke();

    // (Tùy chọn) Sự kiện khi một viên kẹo được click, nếu các hệ thống khác ngoài CandyBoard cần biết
    // public static event Action<Candy> OnCandyClickedEvent;
    // public static void TriggerCandyClicked(Candy candy) => OnCandyClickedEvent?.Invoke(candy);
}