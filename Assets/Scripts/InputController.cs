using UnityEngine;
using System; // Cần thiết để sử dụng Action

/// <summary>
/// Component này chịu trách nhiệm duy nhất cho việc lắng nghe input của người chơi.
/// Nó sử dụng C# events (Observer Pattern) để thông báo cho các hệ thống khác
/// mà không cần biết chúng là ai.
/// </summary>
public class InputController : MonoBehaviour
{
    // Sự kiện được phát đi khi một viên kẹo được click hợp lệ.
    public event Action<Candy> OnCandyClicked;

    // --- Các sự kiện dành cho việc debug ---
    public event Action OnResetBoardPressed;    // Phím Space
    public event Action OnShowStatusPressed;    // Phím S
    public event Action OnFindHintPressed;      // Phím M

    private Camera _mainCamera;

    private void Awake()
    {
        // Cache camera để tối ưu hiệu suất, tránh gọi Camera.main trong Update.
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        // Xử lý click chuột/chạm màn hình
        if (Input.GetMouseButtonDown(0))
        {
            HandleTouch();
        }

        // Xử lý các phím debug
        HandleDebugKeys();
    }

    /// <summary>
    /// Xử lý việc click chuột hoặc chạm vào màn hình.
    /// </summary>
    private void HandleTouch()
    {
        // Chuyển đổi vị trí con trỏ trên màn hình thành một tia trong thế giới game.
        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Candy"));

        if (hit.collider != null)
        {
            Candy candy = hit.collider.gameObject.GetComponent<Candy>();
            if (candy != null)
            {
                // Nếu click trúng một viên kẹo, phát ra sự kiện OnCandyClicked.
                // Dấu '?' đảm bảo code không lỗi nếu không có ai lắng nghe sự kiện này.
                OnCandyClicked?.Invoke(candy);
            }
        }
    }

    /// <summary>
    /// Lắng nghe các phím bấm dùng để debug.
    /// </summary>
    private void HandleDebugKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnResetBoardPressed?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            OnShowStatusPressed?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            OnFindHintPressed?.Invoke();
        }
    }
}