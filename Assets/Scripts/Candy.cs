using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CandyType
{
    Red,
    Yellow,
    Green,
    Purple,
    Blue,
    Orange
}

public class Candy : MonoBehaviour
{
    [SerializeField] public int xIndex;
    [SerializeField] public int yIndex;
    [SerializeField] public bool isMoving;
    [SerializeField] public bool isMatched;
    [SerializeField] public CandyType candyType;

    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        // Đảm bảo có collider
        if (GetComponent<Collider>() == null)
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                collider.size = new Vector3(
                    spriteRenderer.bounds.size.x,
                    spriteRenderer.bounds.size.y,
                    0.1f
                );
            }
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        originalScale = transform.localScale;
    }

    public void Init(int xIndex, int yIndex, CandyType type)
    {
        this.xIndex = xIndex;
        this.yIndex = yIndex;
        this.candyType = type;
        isMatched = false;
        isMoving = false;
    }

    public void setIndicies(int xIndex, int yIndex)
    {
        this.xIndex = xIndex;
        this.yIndex = yIndex;
    }

    public void MoveToTarget(Vector3 targetPosition)
    {
        if (isMoving) return;
        StopAllCoroutines(); // Ngừng các animation đang chạy
        StartCoroutine(MoveToCoroutine(targetPosition));
    }

    private IEnumerator MoveToCoroutine(Vector3 targetPosition)
    {
        isMoving = true;
        float duration = 0.2f;
        Vector3 startPosition = transform.position;
        float time = 0;

        while (time < duration)
        {
            float t = time / duration;
            // Sử dụng công thức easing để chuyển động mượt mà hơn
            float easedT = Mathf.SmoothStep(0, 1, t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
            time += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition; // Đảm bảo đạt đúng vị trí
        isMoving = false;
    }

    public void SetSelected(bool selected)
    {
        if (selected)
        {
            transform.localScale = originalScale * 1.2f;
            // Có thể thêm hiệu ứng phát sáng nếu cần
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f); // Highlight
            }
        }
        else
        {
            transform.localScale = originalScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white; // Trở về bình thường
            }
        }
    }

    void OnMouseDown()
    {
        Debug.Log($"Direct click on candy: Type={candyType}, Position=[{xIndex},{yIndex}]");
        // Bạn có thể xử lý sự kiện click trực tiếp ở đây nếu muốn
    }

    public bool ValidatePosition()
    {
        if (PositionBoard.instance == null) return true;

        Vector3 expectedPosition = new Vector3(
            (xIndex - PositionBoard.instance.spaceingX) * PositionBoard.instance.spacingScale,
            (yIndex - PositionBoard.instance.spaceingY) * PositionBoard.instance.spacingScale,
            transform.position.z
        );

        float distance = Vector3.Distance(transform.position, expectedPosition);
        return distance <= 0.1f;
    }
}