// GameEvents.cs
using System;
using UnityEngine.Events;

/// <summary>
/// Lớp tĩnh trung tâm quản lý tất cả các sự kiện của game.
/// Hoạt động như một trung tâm cho Observer Pattern.
/// </summary>
public static class GameEvents
{
    // Sự kiện được kích hoạt khi điểm số thay đổi. Tham số là điểm số mới.
    public static readonly UnityEvent<int> OnScoreChanged = new UnityEvent<int>();

    // Sự kiện được kích hoạt khi số lượt đi thay đổi. Tham số là số lượt đi còn lại.
    public static readonly UnityEvent<int> OnMovesChanged = new UnityEvent<int>();

    // Sự kiện được kích hoạt khi mục tiêu thay đổi (chủ yếu lúc khởi tạo).
    public static readonly UnityEvent<int> OnGoalChanged = new UnityEvent<int>();

    // Sự kiện khi trò chơi kết thúc với chiến thắng.
    public static readonly UnityEvent OnGameWin = new UnityEvent();

    // Sự kiện khi trò chơi kết thúc với thất bại.
    public static readonly UnityEvent OnGameLose = new UnityEvent();
}