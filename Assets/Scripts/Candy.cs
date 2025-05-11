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

    void Awake()
    {
        if (gameObject == null)
        {
            Debug.LogError("GameObject is null in Candy.Awake");
            return;
        }
        Debug.Log($"Candy {name} Awake start, GameObject: {gameObject.name}");

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
    }

    public void Init(int xIndex, int yIndex, CandyType type, bool isSpecial = false, SpecialCandyEffect effect = SpecialCandyEffect.None)
    {
        this.xIndex = xIndex;
        this.yIndex = yIndex;
        this.candyType = type;
        this.isSpecial = isSpecial;
        this.specialEffect = effect;
        isMatched = false;
        isMoving = false;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (isSpecial)
        {
            if (effect == SpecialCandyEffect.ClearRow)
            {
                gameObject.name = $"LongHorizontal_{type}_{xIndex}_{yIndex}";
            }
            else if (effect == SpecialCandyEffect.ClearColumn)
            {
                gameObject.name = $"LongVertical_{type}_{xIndex}_{yIndex}";
            }
        }
        else
        {
            gameObject.name = $"Candy_{type}_{xIndex}_{yIndex}";
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }
    }

    public void setIndicies(int xIndex, int yIndex)
    {
        Debug.Log($"setIndicies: Candy {candyType} from [{this.xIndex},{this.yIndex}] to [{xIndex},{yIndex}]");
        this.xIndex = xIndex;
        this.yIndex = yIndex;
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
        Debug.Log($"Direct click on candy: Type={candyType}, Position=[{xIndex},{yIndex}]");
        if (CandyBoard.instance != null && !CandyBoard.instance.isProcessingMove)
        {
            CandyBoard.instance.SelectCandy(this);
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

    public void ActivateSpecialEffect()
    {
        if (!isSpecial) return;

        Debug.Log($"Activating special candy effect: {specialEffect} at [{xIndex},{yIndex}]");
        StartCoroutine(PlayActivationEffect());

        if (specialEffect == SpecialCandyEffect.ClearRow)
        {
            Debug.Log($"Activating row clear effect at row {yIndex}");
            StartCoroutine(RowClearEffect(yIndex));
        }
        else if (specialEffect == SpecialCandyEffect.ClearColumn)
        {
            Debug.Log($"Activating column clear effect at column {xIndex}");
            StartCoroutine(ColumnClearEffect(xIndex));
        }
    }

    private IEnumerator PlayActivationEffect()
    {
        if (spriteRenderer != null)
        {
            for (int i = 0; i < 3; i++)
            {
                spriteRenderer.color = new Color(1f, 1f, 0f, spriteRenderer.color.a);
                yield return new WaitForSeconds(0.05f);
                spriteRenderer.color = new Color(1f, 1f, 1f, spriteRenderer.color.a);
                yield return new WaitForSeconds(0.05f);
            }
        }
    }

    private IEnumerator RowClearEffect(int rowIndex)
    {
        CandyBoard.instance.ClearRow(rowIndex);

        GameObject rowBeam = new GameObject("RowBeam");
        rowBeam.transform.position = transform.position;

        SpriteRenderer beamRenderer = rowBeam.AddComponent<SpriteRenderer>();
        beamRenderer.color = new Color(1f, 0.5f, 0.2f, 0.7f);
        beamRenderer.transform.localScale = new Vector3(
            CandyBoard.instance.boardWidth * CandyBoard.instance.spacingScale * 1.5f,
            0.5f,
            1f
        );

        Sprite beamSprite = Resources.Load<Sprite>("Effects/RowClearBeam");
        if (beamSprite != null)
        {
            beamRenderer.sprite = beamSprite;
        }
        else
        {
            Debug.LogWarning("RowClearBeam sprite not found in Resources/Effects. Using default square.");
        }

        float duration = 0.5f;
        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            beamRenderer.color = new Color(1f, 0.5f, 0.2f, 0.7f * (1 - t));
            time += Time.deltaTime;
            yield return null;
        }

        Destroy(rowBeam);
    }

    private IEnumerator ColumnClearEffect(int columnIndex)
    {
        CandyBoard.instance.ClearColumn(columnIndex);

        GameObject columnBeam = new GameObject("ColumnBeam");
        columnBeam.transform.position = transform.position;

        SpriteRenderer beamRenderer = columnBeam.AddComponent<SpriteRenderer>();
        beamRenderer.color = new Color(0.2f, 0.5f, 1f, 0.7f);
        beamRenderer.transform.localScale = new Vector3(
            0.5f,
            CandyBoard.instance.boardHeight * CandyBoard.instance.spacingScale * 1.5f,
            1f
        );

        Sprite beamSprite = Resources.Load<Sprite>("Effects/ColumnClearBeam");
        if (beamSprite != null)
        {
            beamRenderer.sprite = beamSprite;
        }
        else
        {
            Debug.LogWarning("ColumnClearBeam sprite not found in Resources/Effects. Using default square.");
        }

        float duration = 0.5f;
        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            beamRenderer.color = new Color(0.2f, 0.5f, 1f, 0.7f * (1 - t));
            time += Time.deltaTime;
            yield return null;
        }

        Destroy(columnBeam);
    }
}