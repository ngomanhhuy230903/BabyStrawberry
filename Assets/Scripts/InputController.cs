using UnityEngine;
using System;

/// <summary>
/// ĐÃ CẬP NHẬT: Component này bây giờ hoạt động như một "trạm trung chuyển" cho các sự kiện input.
/// Nó không tự thực hiện raycast nữa, mà nhận báo cáo từ các component khác (như Candy)
/// và phát đi sự kiện tương ứng.
/// </summary>
public class InputController : MonoBehaviour
{
    // Thêm một instance Singleton để các viên kẹo có thể dễ dàng truy cập
    public static InputController instance;

    // Sự kiện vẫn được giữ nguyên. CandyBoard vẫn lắng nghe sự kiện này.
    public event Action<Candy> OnCandyClicked;
    public event Action OnResetBoardPressed;
    public event Action OnShowStatusPressed;
    public event Action OnFindHintPressed;

    private void Awake()
    {
        // Khởi tạo Singleton instance
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Chúng ta không cần hàm Update() để Raycast nữa.
    // private void Update() { ... } // Có thể xóa hoặc comment khối lệnh Update cũ đi.

    /// <summary>
    /// Đây là hàm CÔNG KHAI mới mà các viên kẹo sẽ gọi vào.
    /// Khi nhận được báo cáo, nó sẽ phát đi sự kiện OnCandyClicked.
    /// </summary>
    public void ReportCandyClicked(Candy candy)
    {
        OnCandyClicked?.Invoke(candy);
    }

    // Xử lý các phím debug vẫn giữ lại trong Update để tập trung
    private void Update()
    {
        HandleDebugKeys();
    }

    private void HandleDebugKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space)) OnResetBoardPressed?.Invoke();
        if (Input.GetKeyDown(KeyCode.S)) OnShowStatusPressed?.Invoke();
        if (Input.GetKeyDown(KeyCode.M)) OnFindHintPressed?.Invoke();
    }
}