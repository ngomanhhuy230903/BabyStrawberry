using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enums giữ nguyên, không cần thay đổi
public enum CandyType
{
    Red, Yellow, Green, Purple, Blue, Orange
}

public enum SpecialCandyEffect
{
    None,
    ClearRow,
    ClearColumn,
    ClearColor, // Match-5 tạo ra kẹo này
    UpgradeColorToSpecials, // Hiệu ứng khi ColorBomb + kẹo đặc biệt
    ClearBoard // Hiệu ứng khi ColorBomb + ColorBomb
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
    private ISpecialCandyEffectStrategy _effectStrategy;

    [HideInInspector] public Vector3 originalScale;
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) Debug.LogError($"Candy {name} is missing SpriteRenderer component.");

        // Lưu scale gốc
        originalScale = transform.localScale; // THAY ĐỔI/THÊM

        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"Candy {name} is missing Collider2D, adding one automatically.");
            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                boxCollider.size = spriteRenderer.bounds.size;
            }
            else
            {
                boxCollider.size = Vector2.one * 0.7f;
            }
        }

        gameObject.layer = LayerMask.NameToLayer("Candy");
        _effectStrategy = new NoEffectStrategy();
    }

    public void Init(int xIndex, int yIndex, CandyType type, bool isSpecial = false, SpecialCandyEffect effect = SpecialCandyEffect.None)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        this.xIndex = xIndex;
        this.yIndex = yIndex;
        this.candyType = type;
        this.isSpecial = isSpecial;
        this.specialEffect = effect;
        isMatched = false;
        isMoving = false;

        originalScale = transform.localScale; // THÊM: Cập nhật lại originalScale khi Init

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.color = Color.white;

        SetStrategyBasedOnEffect(effect);

        if (isSpecial)
        {
            gameObject.name = $"Special_{effect}_{type}_{xIndex}_{yIndex}";
        }
        else
        {
            gameObject.name = $"Candy_{type}_{xIndex}_{yIndex}";
        }

        StopAllCoroutines(); // THÊM: Quan trọng cho pooling
    }


    public void SetStrategyBasedOnEffect(SpecialCandyEffect effect)
    {
        switch (effect)
        {
            case SpecialCandyEffect.ClearRow:
                _effectStrategy = new ClearRowStrategy();
                break;
            case SpecialCandyEffect.ClearColumn:
                _effectStrategy = new ClearColumnStrategy();
                break;
            case SpecialCandyEffect.ClearColor:
                _effectStrategy = new ClearColorStrategy();
                break;
            case SpecialCandyEffect.UpgradeColorToSpecials:
                _effectStrategy = new UpgradeColorToSpecialsStrategy();
                break;
            case SpecialCandyEffect.ClearBoard:
                _effectStrategy = new ClearBoardStrategy();
                break;
            default:
                _effectStrategy = new NoEffectStrategy();
                break;
        }
    }

    // MODIFIED: Thêm tham số 'otherCandy' để các Strategy có thể biết đối tượng tương tác
    public List<Candy> ExecuteSpecialEffectLogic(CandyBoard board, Candy otherCandy, HashSet<Candy> allCandiesToDestroySet)
    {
        if (!isSpecial || _effectStrategy == null)
        {
            Debug.LogWarning($"ExecuteSpecialEffectLogic called on non-special candy or null strategy: {name}");
            return new List<Candy>();
        }
        ActivateSpecialEffectAndPlayVisuals();
        // Truyền 'otherCandy' vào cho strategy
        return _effectStrategy.Activate(board, this, otherCandy, allCandiesToDestroySet);
    }

    public void setIndicies(int xIndex, int yIndex)
    {
        this.xIndex = xIndex;
        this.yIndex = yIndex;
        // Cập nhật tên để dễ debug
        if (isSpecial) gameObject.name = $"Special_{specialEffect}_{candyType}_{xIndex}_{yIndex}";
        else gameObject.name = $"Candy_{candyType}_{xIndex}_{yIndex}";
    }

    public void MoveToTarget(Vector3 targetPosition)
    {
        if (isMoving && !gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
            isMoving = false;
        }
        if (isMoving) return;
        if (!gameObject.activeInHierarchy) return;

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
            if (!gameObject.activeInHierarchy) // THAY ĐỔI/THÊM
            {
                isMoving = false;
                yield break;
            }
            float t = time / duration;
            transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0, 1, t));
            time += Time.deltaTime;
            yield return null;
        }

        if (gameObject.activeInHierarchy) // THAY ĐỔI/THÊM
        {
            transform.position = targetPosition;
        }
        isMoving = false;
    }

    public void SetSelected(bool selected)
    {
        if (spriteRenderer != null)
        {
            // THAY ĐỔI: Gợi ý chỉ thay đổi màu để tránh xung đột với animation gợi ý scale
            spriteRenderer.color = selected ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
            // Nếu vẫn muốn dùng scale:
            // transform.localScale = selected ? originalScale * 1.1f : originalScale;
        }
    }

    void OnMouseDown()
    {
        // THÊM: Kiểm tra không click xuyên UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (CandyBoard.instance != null)
        {
            CandyBoard.instance.ReportCandyClicked(this);
        }
    }

    public void ActivateSpecialEffectAndPlayVisuals()
    {
        if (!isSpecial || !gameObject.activeInHierarchy) return; // THAY ĐỔI/THÊM
        StartCoroutine(PlayActivationEffect());

        if (specialEffect == SpecialCandyEffect.ClearRow) StartCoroutine(RowClearVisualEffect());
        else if (specialEffect == SpecialCandyEffect.ClearColumn) StartCoroutine(ColumnClearVisualEffect());
    }

    private IEnumerator PlayActivationEffect()
    {
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = new Color(1f, 1f, 0.5f, spriteRenderer.color.a);
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null) spriteRenderer.color = originalColor; // THAY ĐỔI/THÊM: Kiểm tra null
        }
    }

    private IEnumerator RowClearVisualEffect()
    {
        if (CandyBoard.instance == null || !gameObject.activeInHierarchy) yield break; // THAY ĐỔI/THÊM
        GameObject rowBeam = new GameObject("RowBeamVisual");
        rowBeam.transform.position = this.transform.position;

        SpriteRenderer beamRenderer = rowBeam.AddComponent<SpriteRenderer>();
        // THÊM: Tạo sprite mặc định cho beam nếu không có
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        beamRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);

        beamRenderer.color = new Color(1f, 0.8f, 0.2f, 0.7f);
        float beamWidth = CandyBoard.instance.boardWidth * CandyBoard.instance.spacingScale;
        rowBeam.transform.localScale = new Vector3(beamWidth, 0.3f * CandyBoard.instance.spacingScale, 1f);

        float duration = 0.3f;
        float time = 0;
        while (time < duration)
        {
            if (!gameObject.activeInHierarchy) { Destroy(rowBeam); yield break; } // THAY ĐỔI/THÊM
            float t = time / duration;
            beamRenderer.color = new Color(beamRenderer.color.r, beamRenderer.color.g, beamRenderer.color.b, 0.7f * (1 - t));
            time += Time.deltaTime;
            yield return null;
        }
        Destroy(rowBeam);
    }

    private IEnumerator ColumnClearVisualEffect()
    {
        if (CandyBoard.instance == null || !gameObject.activeInHierarchy) yield break; // THAY ĐỔI/THÊM
        GameObject columnBeam = new GameObject("ColumnBeamVisual");
        columnBeam.transform.position = this.transform.position;
        SpriteRenderer beamRenderer = columnBeam.AddComponent<SpriteRenderer>();
        // THÊM: Tạo sprite mặc định
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        beamRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);

        beamRenderer.color = new Color(0.2f, 0.8f, 1f, 0.7f);
        float beamHeight = CandyBoard.instance.boardHeight * CandyBoard.instance.spacingScale;
        columnBeam.transform.localScale = new Vector3(0.3f * CandyBoard.instance.spacingScale, beamHeight, 1f);

        float duration = 0.3f;
        float time = 0;
        while (time < duration)
        {
            if (!gameObject.activeInHierarchy) { Destroy(columnBeam); yield break; } // THAY ĐỔI/THÊM
            float t = time / duration;
            beamRenderer.color = new Color(beamRenderer.color.r, beamRenderer.color.g, beamRenderer.color.b, 0.7f * (1 - t));
            time += Time.deltaTime;
            yield return null;
        }
        Destroy(columnBeam);
    }
}