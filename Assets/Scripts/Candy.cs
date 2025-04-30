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

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        if (gameObject == null)
        {
            Debug.LogError("GameObject is null in Candy.Awake");
            return;
        }
        Debug.Log($"Candy {name} Awake start, GameObject: {gameObject.name}");

        // Kiểm tra xem có Collider2D (cho 2D) hay không
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D == null)
        {
            Debug.Log("Adding BoxCollider2D to Candy");
            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Debug.Log($"SpriteRenderer found: {spriteRenderer.name}, Sprite: {spriteRenderer.sprite.name}");
                boxCollider.size = new Vector2(
                    spriteRenderer.bounds.size.x,
                    spriteRenderer.bounds.size.y
                );
            }
            else
            {
                Debug.LogWarning($"Candy {name} is missing SpriteRenderer or sprite, using default collider size.");
                boxCollider.size = new Vector2(1f, 1f);
            }
        }
        else
        {
            Debug.Log($"Candy {name} already has a Collider2D: {collider2D.GetType().Name}");
        }

        // Kiểm tra SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"Candy {name} is missing SpriteRenderer component after second check. GameObject: {gameObject.name}, Active: {gameObject.activeSelf}");
        }
        else
        {
            Debug.Log($"SpriteRenderer found after second check: {spriteRenderer.name}");
            if (spriteRenderer.sprite == null)
            {
                Debug.LogError($"Candy {name} has SpriteRenderer but no sprite assigned after second check.");
            }
            else
            {
                Debug.Log($"Sprite assigned after second check: {spriteRenderer.sprite.name}");
            }
        }
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
        if (spriteRenderer != null)
        {
            spriteRenderer.color = selected ? new Color(1f, 1f, 1f, 1f) : Color.white; // Highlight hoặc trở về bình thường
        }
        Debug.Log($"Candy {candyType} at [{xIndex},{yIndex}] {(selected ? "selected" : "deselected")}");
    }

    void OnMouseDown()
    {
        Debug.Log($"Direct click on candy: Type={candyType}, Position=[{xIndex},{yIndex}]");
        // Gọi luôn SelectCandy để đồng bộ với logic swap
        if (PositionBoard.instance != null && !PositionBoard.instance.isProcessingMove)
        {
            PositionBoard.instance.SelectCandy(this);
        }
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