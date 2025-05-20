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

public enum SpecialCandyEffect
{
    None,
    ClearRow,
    ClearColumn
}

public class Candy : MonoBehaviour
{
    [SerializeField] public int xIndex;
    [SerializeField] public int yIndex;
    [SerializeField] public bool isMoving;
    [SerializeField] public bool isMatched;
    [SerializeField] public CandyType candyType;
    [SerializeField] public bool isSpecial;
    [SerializeField] public SpecialCandyEffect specialEffect;

    private SpriteRenderer spriteRenderer;
    private ISpecialCandyEffectStrategy _effectStrategy; // THAY ĐỔI: Trường giữ strategy


    void Awake()
    {
        if (gameObject == null)
        {
            Debug.LogError("GameObject is null in Candy.Awake");
            return;
        }
        Debug.Log($"Candy {name} Awake, GameObject: {gameObject.name}");

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

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"Candy {name} is missing SpriteRenderer component.");
        }
        else
        {
            Debug.Log($"SpriteRenderer found: {spriteRenderer.name}");
            if (spriteRenderer.sprite == null)
            {
                Debug.LogError($"Candy {name} has SpriteRenderer but no sprite assigned.");
            }
            else
            {
                Debug.Log($"Sprite assigned: {spriteRenderer.sprite.name}");
            }
        }
        gameObject.layer = LayerMask.NameToLayer("Candy");
        if (_effectStrategy == null)
        {
            _effectStrategy = new NoEffectStrategy();
        }
    }
    public void Init(int xIndex, int yIndex, CandyType type, bool isSpecial = false, SpecialCandyEffect effect = SpecialCandyEffect.None)
    {
        // Quan trọng: Đảm bảo GameObject active khi lấy từ pool và Init
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        this.xIndex = xIndex;
        this.yIndex = yIndex;
        this.candyType = type;
        this.isSpecial = isSpecial;
        this.specialEffect = effect;
        isMatched = false;
        isMoving = false;

        // Reset SpriteRenderer (quan trọng cho pooling)
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>(); // Đảm bảo cache
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white; // Reset màu về mặc định (hoặc màu prefab gốc)
            // Nếu prefab có sprite khác nhau cho các loại kẹo, bạn cần logic để set đúng sprite ở đây
            // hoặc đảm bảo prefab được lấy từ pool đã có sprite đúng.
            // Ví dụ: spriteRenderer.sprite = GetSpriteForType(type);
        }

        SetStrategyBasedOnEffect(effect); // Gán strategy

        // Đặt tên GameObject (giữ nguyên logic đặt tên của bạn)
        if (isSpecial)
        {
            if (effect == SpecialCandyEffect.ClearRow) gameObject.name = $"Special_Row_{type}_{xIndex}_{yIndex}";
            else if (effect == SpecialCandyEffect.ClearColumn) gameObject.name = $"Special_Col_{type}_{xIndex}_{yIndex}";
            // ... các loại special khác
            else gameObject.name = $"Special_Unknown_{type}_{xIndex}_{yIndex}";
        }
        else
        {
            gameObject.name = $"Candy_{type}_{xIndex}_{yIndex}";
        }

        // Dừng tất cả coroutine từ lần sử dụng trước (rất quan trọng cho pooling)
        StopAllCoroutines();
        // Debug.Log($"Candy Init: {gameObject.name}, Active: {gameObject.activeSelf}");
    }
    private void SetStrategyBasedOnEffect(SpecialCandyEffect effect)
    {
        switch (effect)
        {
            case SpecialCandyEffect.ClearRow:
                _effectStrategy = new ClearRowStrategy();
                break;
            case SpecialCandyEffect.ClearColumn:
                _effectStrategy = new ClearColumnStrategy();
                break;
            case SpecialCandyEffect.None:
            default:
                _effectStrategy = new NoEffectStrategy(); // Mặc định là không có hiệu ứng
                break;
        }
    }

    // THAY ĐỔI: Phương thức mới để thực thi logic hiệu ứng đặc biệt
    public List<Candy> ExecuteSpecialEffectLogic(CandyBoard board, HashSet<Candy> allCandiesToDestroySet)
    {
        if (!isSpecial || _effectStrategy == null)
        {
            Debug.LogWarning($"ExecuteSpecialEffectLogic called on non-special candy or null strategy: {name}");
            return new List<Candy>(); // Trả về danh sách rỗng nếu không phải special hoặc không có strategy
        }

        // Kích hoạt hiệu ứng hình ảnh (flash, beam visuals)
        // Hàm này giờ chỉ tập trung vào hình ảnh
        ActivateSpecialEffectAndPlayVisuals();

        // Ủy thác logic cốt lõi cho strategy
        // Strategy sẽ cập nhật allCandiesToDestroySet và trả về các kẹo mới bị ảnh hưởng
        return _effectStrategy.Activate(board, this, allCandiesToDestroySet);
    }
    public void setIndicies(int xIndex, int yIndex)
    {
        Debug.Log($"setIndicies: Candy {candyType} from [{this.xIndex},{this.yIndex}] to [{xIndex},{yIndex}]");
        this.xIndex = xIndex;
        this.yIndex = yIndex;

        // Update gameObject name to reflect new indices
        if (isSpecial)
        {
            if (specialEffect == SpecialCandyEffect.ClearRow)
            {
                gameObject.name = $"LongHorizontal_{candyType}_{xIndex}_{yIndex}";
            }
            else if (specialEffect == SpecialCandyEffect.ClearColumn)
            {
                gameObject.name = $"LongVertical_{candyType}_{xIndex}_{yIndex}";
            }
        }
        else
        {
            gameObject.name = $"Candy_{candyType}_{xIndex}_{yIndex}";
        }
    }

    public void MoveToTarget(Vector3 targetPosition)
    {
        if (isMoving) return;
        StopAllCoroutines();
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
            float easedT = Mathf.SmoothStep(0, 1, t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
            time += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
    }

    public void SetSelected(bool selected)
    {
        if (spriteRenderer != null)
        {
            Color currentColor = spriteRenderer.color;
            spriteRenderer.color = selected ? new Color(currentColor.r, currentColor.g, currentColor.b, 0.7f) : new Color(currentColor.r, currentColor.g, currentColor.b, 1f);
        }
        Debug.Log($"Candy {candyType} at [{xIndex},{yIndex}] {(selected ? "selected" : "deselected")}");
    }

    void OnMouseDown()
    {
        Debug.Log($"OnMouseDown: Candy {candyType} at [{xIndex},{yIndex}], isSpecial={isSpecial}, isMoving={isMoving}");

        // THAY ĐỔI: Ủy thác việc xử lý click cho CandyBoard, sau đó CandyBoard sẽ chuyển cho state hiện tại
        if (CandyBoard.instance != null) // Không cần kiểm tra isProcessingMove ở đây nữa, state sẽ quyết định
        {
            CandyBoard.instance.ReportCandyClicked(this);
        }
        else
        {
            Debug.LogWarning($"Cannot select candy: CandyBoard.instance is null");
        }
    }


    public bool ValidatePosition()
    {
        if (CandyBoard.instance == null) return true;

        Vector3 expectedPosition = new Vector3(
            (xIndex - CandyBoard.instance.spaceingX) * CandyBoard.instance.spacingScale,
            (yIndex - CandyBoard.instance.spaceingY) * CandyBoard.instance.spacingScale,
            transform.position.z
        );

        float distance = Vector3.Distance(transform.position, expectedPosition);
        return distance <= 0.1f;
    }

    // In Candy.cs

    // Rename for clarity, this is now primarily for visual effects when activated by a match.
    public void ActivateSpecialEffectAndPlayVisuals()
    {
        if (!isSpecial) return;

        Debug.Log($"Playing activation visuals for: {specialEffect} at [{xIndex},{yIndex}]");
        StartCoroutine(PlayActivationEffect()); // The flash

        // Trigger the beam visuals. These should be purely visual and not call game logic.
        if (specialEffect == SpecialCandyEffect.ClearRow)
        {
            Debug.Log($"Playing row clear visual effect at row {yIndex}");
            StartCoroutine(RowClearVisualEffect());
        }
        else if (specialEffect == SpecialCandyEffect.ClearColumn)
        {
            Debug.Log($"Playing column clear visual effect at column {xIndex}");
            StartCoroutine(ColumnClearVisualEffect());
        }
    }

    // This coroutine is for the initial flash/highlight of the special candy itself
    private IEnumerator PlayActivationEffect()
    {
        if (spriteRenderer != null)
        {
            // Example: Simple flash
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = new Color(1f, 1f, 0.5f, spriteRenderer.color.a); // Yellowish highlight
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = new Color(1f, 1f, 0.5f, spriteRenderer.color.a);
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
        }
    }

    // Modified to be purely visual, NO CandyBoard.instance.ClearRow calls
    private IEnumerator RowClearVisualEffect()
    {
        // CandyBoard.instance.ClearRow(rowIndex); // REMOVED - Logic is now in CandyBoard.RemoveAndRefill

        GameObject rowBeam = new GameObject("RowBeamVisual");
        rowBeam.transform.position = this.transform.position; // Centered on the special candy

        SpriteRenderer beamRenderer = rowBeam.AddComponent<SpriteRenderer>();
        // Configure your beam's appearance (sprite, color, scale, etc.)
        // Example:
        beamRenderer.color = new Color(1f, 0.8f, 0.2f, 0.7f); // Orange-ish
                                                              // Adjust scale to cover the row. You might need to get board dimensions from CandyBoard.instance
        float beamWidth = CandyBoard.instance.boardWidth * CandyBoard.instance.spacingScale;
        beamRenderer.transform.localScale = new Vector3(beamWidth, 0.3f * CandyBoard.instance.spacingScale, 1f);
        // Assign a sprite if you have one, e.g., a white square or a custom beam sprite
        // beamRenderer.sprite = Resources.Load<Sprite>("PathToYourBeamSprite");

        float duration = 0.3f; // Visual effect duration
        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            beamRenderer.color = new Color(beamRenderer.color.r, beamRenderer.color.g, beamRenderer.color.b, 0.7f * (1 - t)); // Fade out
                                                                                                                              // Optional: Animate scale or position
            time += Time.deltaTime;
            yield return null;
        }
        Destroy(rowBeam);
    }

    // Modified to be purely visual, NO CandyBoard.instance.ClearColumn calls
    private IEnumerator ColumnClearVisualEffect()
    {
        // CandyBoard.instance.ClearColumn(columnIndex); // REMOVED

        GameObject columnBeam = new GameObject("ColumnBeamVisual");
        columnBeam.transform.position = this.transform.position;

        SpriteRenderer beamRenderer = columnBeam.AddComponent<SpriteRenderer>();
        beamRenderer.color = new Color(0.2f, 0.8f, 1f, 0.7f); // Bluish
        float beamHeight = CandyBoard.instance.boardHeight * CandyBoard.instance.spacingScale;
        beamRenderer.transform.localScale = new Vector3(0.3f * CandyBoard.instance.spacingScale, beamHeight, 1f);
        // beamRenderer.sprite = Resources.Load<Sprite>("PathToYourBeamSprite");


        float duration = 0.3f;
        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            beamRenderer.color = new Color(beamRenderer.color.r, beamRenderer.color.g, beamRenderer.color.b, 0.7f * (1 - t));
            time += Time.deltaTime;
            yield return null;
        }
        Destroy(columnBeam);
    }
}