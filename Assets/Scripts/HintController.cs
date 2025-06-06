using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý toàn bộ hệ thống gợi ý. Tự đếm thời gian và hiển thị animation gợi ý
/// khi người chơi không tương tác.
/// </summary>
public class HintController : MonoBehaviour
{
    [Header("Hint Settings")]
    [Tooltip("Thời gian (giây) chờ trước khi hiển thị gợi ý.")]
    public float hintDelay = 5f;
    [Tooltip("Thời gian (giây) hiển thị hoạt ảnh gợi ý.")]
    public float hintDisplayDuration = 1.5f;

    // Tham chiếu đến các hệ thống khác
    private CandyBoard _board;
    private BoardMatcher _boardMatcher;

    // Các biến nội bộ
    private List<Candy> _hintCandies = new List<Candy>();
    private Coroutine _hintAnimationCoroutine;
    private Coroutine _hintDelayCoroutine;

    /// <summary>
    /// Cung cấp các tham chiếu cần thiết cho HintController hoạt động.
    /// Sẽ được gọi bởi CandyBoard.
    /// </summary>
    public void Initialize(CandyBoard board, BoardMatcher matcher)
    {
        _board = board;
        _boardMatcher = matcher;
    }

    /// <summary>
    /// Bắt đầu đếm ngược thời gian để hiển thị gợi ý.
    /// Được gọi khi board chuyển sang trạng thái Idle.
    /// </summary>
    public void StartIdleTimer()
    {
        ResetIdleTimer(); // Đảm bảo timer cũ đã được dừng
        _hintDelayCoroutine = StartCoroutine(HintDelayCoroutine());
    }

    /// <summary>
    /// Dừng tất cả các hoạt động gợi ý (cả timer và animation).
    /// Được gọi khi người chơi tương tác hoặc board thay đổi trạng thái.
    /// </summary>
    public void ResetIdleTimer()
    {
        if (_hintDelayCoroutine != null)
        {
            StopCoroutine(_hintDelayCoroutine);
            _hintDelayCoroutine = null;
        }
        StopHintAnimation();
    }

    /// <summary>
    /// Coroutine đếm ngược thời gian trước khi tìm và hiển thị gợi ý.
    /// </summary>
    private IEnumerator HintDelayCoroutine()
    {
        yield return new WaitForSeconds(hintDelay);

        // Sau khi chờ, tìm một nước đi gợi ý
        List<Candy> possibleMove = _boardMatcher.FindHint(_board.candyBoard);
        if (possibleMove != null && possibleMove.Count == 2)
        {
            ShowHintAnimation(possibleMove);
        }
        _hintDelayCoroutine = null;
    }

    /// <summary>
    /// Bắt đầu coroutine hiển thị animation cho các viên kẹo gợi ý.
    /// </summary>
    private void ShowHintAnimation(List<Candy> candiesToShow)
    {
        if (candiesToShow == null || candiesToShow.Count != 2 || _hintAnimationCoroutine != null) return;
        if (candiesToShow[0] == null || candiesToShow[1] == null) return;

        _hintCandies = new List<Candy>(candiesToShow);
        _hintAnimationCoroutine = StartCoroutine(HintAnimationCoroutine());
    }

    /// <summary>
    /// Dừng animation gợi ý và reset trạng thái của các viên kẹo.
    /// </summary>
    private void StopHintAnimation()
    {
        if (_hintAnimationCoroutine != null)
        {
            StopCoroutine(_hintAnimationCoroutine);
            _hintAnimationCoroutine = null;

            foreach (Candy c in _hintCandies)
            {
                if (c != null && c.gameObject.activeSelf)
                {
                    // Reset lại scale hoặc bất kỳ hiệu ứng nào khác
                    c.transform.localScale = Vector3.one;
                }
            }
            _hintCandies.Clear();
        }
    }

    /// <summary>
    /// Coroutine thực hiện hoạt ảnh "rung lắc" hoặc "phóng to/thu nhỏ" cho kẹo.
    /// </summary>
    private IEnumerator HintAnimationCoroutine()
    {
        Candy candy1 = _hintCandies[0];
        Candy candy2 = _hintCandies[1];

        float timer = 0f;
        Vector3 originalScale1 = candy1.transform.localScale;
        Vector3 originalScale2 = candy2.transform.localScale;
        float animationSpeed = 2.5f;
        float scaleAmount = 0.12f;

        while (timer < hintDisplayDuration)
        {
            // Kiểm tra liên tục nếu kẹo bị hủy hoặc game over
            if (candy1 == null || !candy1.gameObject.activeSelf || candy2 == null || !candy2.gameObject.activeSelf)
            {
                ResetIdleTimer(); // Dừng mọi thứ nếu có sự thay đổi
                yield break;
            }

            float scaleFactor = 1 + (Mathf.Sin(Time.time * Mathf.PI * animationSpeed) * scaleAmount);
            candy1.transform.localScale = originalScale1 * scaleFactor;
            candy2.transform.localScale = originalScale2 * scaleFactor;

            timer += Time.deltaTime;
            yield return null;
        }

        // Reset scale và dọn dẹp
        if (candy1 != null) candy1.transform.localScale = originalScale1;
        if (candy2 != null) candy2.transform.localScale = originalScale2;

        _hintCandies.Clear();
        _hintAnimationCoroutine = null;

        // Sau khi một gợi ý hoàn thành, tự động bắt đầu lại bộ đếm cho gợi ý tiếp theo
        StartIdleTimer();
    }
}