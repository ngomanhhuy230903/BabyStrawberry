using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CandyBoard : MonoBehaviour
{
    public int boardWidth = 6;
    public int boardHeight = 8;
    public float spaceingX;
    public float spaceingY;
    public float spacingScale = 1.5f;
    public GameObject[] candyPrefab;
    public Node[,] candyBoard;
    public GameObject candyBoardGO;
    public List<GameObject> candyToDestroy = new();
    public GameObject candyParent;
    [SerializeField] private Candy selectedCandy;
    [SerializeField] public bool isProcessingMove;
    [SerializeField] List<Candy> candyToRemove = new List<Candy>();
    public ArrayLayout arrayLayout;
    public static CandyBoard instance;

    public void Awake()
    {
        instance = this;
    }

    public void Start()
    {
        InitializeBoard();
    }

    public void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
            if (hit.collider != null)
            {
                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
                if (candy != null && !isProcessingMove)
                {
                    Debug.Log($"Click candy at position [{candy.xIndex},{candy.yIndex}], type: {candy.candyType}");
                    SelectCandy(candy);
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            InitializeBoard();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (selectedCandy != null)
                Debug.Log($"Current selectedCandy: {selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]");
            else
                Debug.Log("No candy selected");
        }
    }

    public void InitializeBoard()
    {
        DestroyPosition();
        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2) + 1;
        spaceingY = (float)((boardHeight - 1) / 2) - 1;

        if (candyPrefab == null || candyPrefab.Length == 0)
        {
            Debug.LogError("candyBoard: candyPrefab array is null or empty. Please assign candy prefabs in the Inspector.");
            return;
        }

        for (int i = 0; i < candyPrefab.Length; i++)
        {
            if (candyPrefab[i] == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is null. Please assign a valid prefab in the Inspector.");
                return;
            }
            if (candyPrefab[i].GetComponent<Candy>() == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is missing Candy component.");
                return;
            }
            SpriteRenderer sr = candyPrefab[i].GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is missing SpriteRenderer component.");
                return;
            }
            if (sr.sprite == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] has SpriteRenderer but no sprite assigned.");
                return;
            }
        }

        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight)
        {
            Debug.LogError("candyBoard: arrayLayout is null or has incorrect row count. Please configure ArrayLayout in the Inspector.");
            return;
        }
        for (int y = 0; y < boardHeight; y++)
        {
            if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth)
            {
                Debug.LogError($"candyBoard: arrayLayout.rows[{y}].row is null or has incorrect length.");
                return;
            }
        }

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                Vector3 position = new Vector3((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale, 0);
                if (arrayLayout.rows[y].row[x])
                {
                    candyBoard[x, y] = new Node(false, null);
                }
                else
                {
                    int randomIndex = Random.Range(0, candyPrefab.Length);
                    Debug.Log($"Instantiating candy at [{x},{y}] with prefab: {candyPrefab[randomIndex].name}");
                    GameObject candy = Instantiate(candyPrefab[randomIndex], position, Quaternion.identity);
                    candy.transform.SetParent(candyParent.transform);
                    if (candy == null)
                    {
                        Debug.LogError($"Failed to instantiate candy at [{x},{y}]");
                        continue;
                    }
                    Candy candyComponent = candy.GetComponent<Candy>();
                    if (candyComponent == null)
                    {
                        Debug.LogError($"Candy at [{x},{y}] is missing Candy component after instantiation.");
                        Destroy(candy);
                        continue;
                    }
                    candyComponent.setIndicies(x, y);
                    candyComponent.Init(x, y, (CandyType)randomIndex);
                    candyBoard[x, y] = new Node(true, candy);
                    candyToDestroy.Add(candy);
                }
            }
        }
    }

    private void DestroyPosition()
    {
        if (candyToDestroy != null)
        {
            foreach (GameObject position in candyToDestroy)
            {
                Destroy(position);
            }
            candyToDestroy.Clear();
        }
    }

    public bool CheckBoard()
    {
        if (GameManager.instance.isGameOver)
        {
            return false;
        }
        Debug.Log("CheckBoard");
        bool hasMatch = false;
        candyToRemove.Clear();
        foreach (Node nodeCandy in candyBoard)
        {
            if (nodeCandy.candy != null)
            {
                nodeCandy.candy.GetComponent<Candy>().isMatched = false;
            }
        }
        try
        {
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
                    {
                        Candy candy = candyBoard[x, y].candy.GetComponent<Candy>();
                        if (candy == null)
                        {
                            Debug.LogWarning($"Candy component not found at position [{x},{y}]");
                            continue;
                        }
                        if (!candy.isMatched)
                        {
                            MatchResult matchCandy = IsConnected(candy);
                            if (matchCandy != null && matchCandy.connectionCandys != null && matchCandy.connectionCandys.Count >= 3)
                            {
                                MatchResult superMatchCandys = SuperMatch(matchCandy);
                                candyToRemove.AddRange(superMatchCandys.connectionCandys);
                                foreach (Candy c in superMatchCandys.connectionCandys)
                                {
                                    c.isMatched = true;
                                }
                                hasMatch = true;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in CheckBoard: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        return hasMatch;
    }

    public IEnumerator ProcessTurnOnMatchedBoard(bool subtractMoves)
    {
        foreach (Candy candy in candyToRemove)
        {
            candy.isMatched = false;
        }
        RemoveAndRefill(candyToRemove);
        GameManager.instance.ProcessTurn(candyToRemove.Count, subtractMoves);
        yield return new WaitForSeconds(0.4f);
        if (CheckBoard())
        {
            StartCoroutine(ProcessTurnOnMatchedBoard(false));
        }
    }

    #region Cascading Candys
    private void RemoveAndRefill(List<Candy> candyToRemove)
    {
        foreach (Candy candy in candyToRemove)
        {
            int xIndex = candy.xIndex;
            int yIndex = candy.yIndex;
            Destroy(candy.gameObject);
            candyBoard[xIndex, yIndex] = new Node(true, null);
        }
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (candyBoard[x, y].candy == null)
                {
                    Debug.Log($"The location X: {x} Y: {y} is empty, attempting to refill it");
                    RefillCandy(x, y);
                }
            }
        }
        CheckBoard();
        Debug.Log("Refill complete");
    }

    private void RefillCandy(int x, int y)
    {
        int yOffset = 1;
        while (y + yOffset < boardHeight && candyBoard[x, y + yOffset].candy == null)
        {
            yOffset++;
        }
        if (y + yOffset < boardHeight && candyBoard[x, y + yOffset].candy != null)
        {
            Candy candyAbove = candyBoard[x, y + yOffset].candy.GetComponent<Candy>();
            Vector3 targetPos = new Vector3(
                (x - spaceingX) * spacingScale,
                (y - spaceingY) * spacingScale,
                candyAbove.transform.position.z
            );
            Debug.Log($"Moving candy from X: {x} Y: {y + yOffset} to X: {x} Y: {y}, TargetPos: {targetPos}");
            candyAbove.MoveToTarget(targetPos);
            candyAbove.setIndicies(x, y);
            candyBoard[x, y] = candyBoard[x, y + yOffset];
            candyBoard[x, y + yOffset] = new Node(true, null);
            if (y + yOffset < boardHeight)
            {
                RefillCandy(x, y + yOffset);
            }
        }
        else if (y + yOffset >= boardHeight)
        {
            Debug.Log($"Reached the top of column X: {x} at Y: {y}, spawning new candy");
            SpawnCandyAtTop(x, y);
        }
    }

    private void SpawnCandyAtTop(int x, int y)
    {
        int index = FindIndexOfLowestNull(x);
        if (index == -1 || index < y)
        {
            Debug.LogWarning($"No valid empty slot found in column X: {x} for Y: {y}, skipping spawn");
            return;
        }
        Debug.Log($"Spawning candy in column X: {x} at index Y: {index}");
        int randomIndex = Random.Range(0, candyPrefab.Length);
        Vector3 spawnPos = new Vector3(
            (x - spaceingX) * spacingScale,
            (boardHeight - spaceingY) * spacingScale,
            0
        );
        GameObject newCandy = Instantiate(candyPrefab[randomIndex], spawnPos, Quaternion.identity);
        newCandy.transform.SetParent(candyParent.transform);
        Candy candyComponent = newCandy.GetComponent<Candy>();
        candyComponent.setIndicies(x, index);
        Vector3 targetPos = new Vector3(
            (x - spaceingX) * spacingScale,
            (index - spaceingY) * spacingScale,
            0
        );
        Debug.Log($"Moving new candy to X: {x} Y: {index}, TargetPos: {targetPos}");
        candyComponent.MoveToTarget(targetPos);
        candyBoard[x, index] = new Node(true, newCandy);
    }

    private int FindIndexOfLowestNull(int x)
    {
        for (int y = 0; y < boardHeight; y++)
        {
            if (candyBoard[x, y].candy == null)
            {
                return y;
            }
        }
        return -1;
    }
    #endregion

    private MatchResult SuperMatch(MatchResult matchCandy)
    {
        if (matchCandy.direction == MatchDirection.Horizontal || matchCandy.direction == MatchDirection.LongHorizontal)
        {
            foreach (Candy candy in matchCandy.connectionCandys)
            {
                List<Candy> extraConnectionCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.up, extraConnectionCandys);
                CheckDirection(candy, Vector2Int.down, extraConnectionCandys);
                if (extraConnectionCandys.Count >= 2)
                {
                    Debug.Log("I have a super horizontal match, the color is match is: " + matchCandy.connectionCandys[0].candyType);
                    extraConnectionCandys.AddRange(matchCandy.connectionCandys);
                    return new MatchResult() { connectionCandys = extraConnectionCandys, direction = MatchDirection.Super };
                }
            }
            return new MatchResult() { connectionCandys = matchCandy.connectionCandys, direction = matchCandy.direction };
        }
        else if (matchCandy.direction == MatchDirection.Vertical || matchCandy.direction == MatchDirection.LongVertical)
        {
            foreach (Candy candy in matchCandy.connectionCandys)
            {
                List<Candy> extraConnectionCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.right, extraConnectionCandys);
                CheckDirection(candy, Vector2Int.left, extraConnectionCandys);
                if (extraConnectionCandys.Count >= 2)
                {
                    Debug.Log("I have a super vertical match, the color is match is: " + matchCandy.connectionCandys[0].candyType);
                    extraConnectionCandys.AddRange(matchCandy.connectionCandys);
                    return new MatchResult() { connectionCandys = extraConnectionCandys, direction = MatchDirection.Super };
                }
            }
            return new MatchResult() { connectionCandys = matchCandy.connectionCandys, direction = matchCandy.direction };
        }
        return null;
    }

    MatchResult IsConnected(Candy candy)
    {
        List<Candy> connectionCandys = new List<Candy>();
        CandyType candyType = candy.candyType;
        connectionCandys.Add(candy);
        CheckDirection(candy, Vector2Int.right, connectionCandys);
        CheckDirection(candy, Vector2Int.left, connectionCandys);
        if (connectionCandys.Count == 3)
        {
            Debug.Log("I have a normal horizontal match,the color is match is: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.Horizontal };
        }
        else if (connectionCandys.Count > 3)
        {
            Debug.Log("I have a long horizontal match,the color is match is: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.LongHorizontal };
        }
        connectionCandys.Clear();
        connectionCandys.Add(candy);
        CheckDirection(candy, Vector2Int.up, connectionCandys);
        CheckDirection(candy, Vector2Int.down, connectionCandys);
        if (connectionCandys.Count == 3)
        {
            Debug.Log("I have a normal Vertical match,the color is match is: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.Vertical };
        }
        else if (connectionCandys.Count > 3)
        {
            Debug.Log("I have a long vertical match,the color is match is: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.LongVertical };
        }
        else
        {
            return new MatchResult() { connectionCandys = new List<Candy>(), direction = MatchDirection.None };
        }
    }

    void CheckDirection(Candy candy, Vector2Int direction, List<Candy> connectionCandys)
    {
        CandyType candyType = candy.candyType;
        int x = candy.xIndex + direction.x;
        int y = candy.yIndex + direction.y;
        while (x >= 0 && x < boardWidth && y >= 0 && y < boardHeight)
        {
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
            {
                Candy nextCandy = candyBoard[x, y].candy.GetComponent<Candy>();
                if (!nextCandy.isMatched && nextCandy.candyType == candyType)
                {
                    connectionCandys.Add(nextCandy);
                    x += direction.x;
                    y += direction.y;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
    }

    #region Swapping candys
    public void SelectCandy(Candy candy)
    {
        if (candy == null)
        {
            Debug.LogError("Cannot select null candy");
            return;
        }
        Debug.Log($"SelectCandy called with candy type {candy.candyType} at [{candy.xIndex},{candy.yIndex}]");
        Debug.Log($"Current selectedCandy: {(selectedCandy == null ? "null" : selectedCandy.candyType.ToString() + " at [" + selectedCandy.xIndex + "," + selectedCandy.yIndex + "]")}");
        if (isProcessingMove)
        {
            Debug.Log("Still processing previous move");
            return;
        }
        if (selectedCandy == null)
        {
            selectedCandy = candy;
            selectedCandy.SetSelected(true);
            Debug.Log($"First candy selected: {selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]");
        }
        else if (selectedCandy == candy)
        {
            selectedCandy.SetSelected(false);
            selectedCandy = null;
            Debug.Log("Deselected candy");
        }
        else
        {
            Debug.Log($"Second candy selected: {candy.candyType} at [{candy.xIndex},{candy.yIndex}], attempting swap");
            Candy firstCandy = selectedCandy;
            Candy secondCandy = candy;
            firstCandy.SetSelected(false);
            selectedCandy = null;
            SwapCandy(firstCandy, secondCandy);
        }
    }

    private void SwapCandy(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log($"Attempting to swap: {firstCandy.candyType}[{firstCandy.xIndex},{firstCandy.yIndex}] with {secondCandy.candyType}[{secondCandy.xIndex},{secondCandy.yIndex}]");
        if (!IsAdjacent(firstCandy, secondCandy))
        {
            Debug.LogWarning("Candies are not adjacent!");
            return;
        }
        isProcessingMove = true;
        DoSwap(firstCandy, secondCandy);
        StartCoroutine(ProcessMatches(firstCandy, secondCandy));
    }

    public void DoSwap(Candy firstCandy, Candy secondCandy)
    {
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("Cannot swap with null candy");
            isProcessingMove = false;
            return;
        }
        int firstX = firstCandy.xIndex;
        int firstY = firstCandy.yIndex;
        int secondX = secondCandy.xIndex;
        int secondY = secondCandy.yIndex;
        Debug.Log($"DoSwap: Swapping [{firstX},{firstY}] with [{secondX},{secondY}]");
        if (firstX < 0 || firstX >= boardWidth || firstY < 0 || firstY >= boardHeight ||
            secondX < 0 || secondX >= boardWidth || secondY < 0 || secondY >= boardHeight)
        {
            Debug.LogError("Invalid indices for candy swap");
            isProcessingMove = false;
            return;
        }
        Vector3 firstPos = firstCandy.transform.position;
        Vector3 secondPos = secondCandy.transform.position;
        candyBoard[firstX, firstY].candy = secondCandy.gameObject;
        candyBoard[secondX, secondY].candy = firstCandy.gameObject;
        firstCandy.setIndicies(secondX, secondY);
        secondCandy.setIndicies(firstX, firstY);
        firstCandy.MoveToTarget(secondPos);
        secondCandy.MoveToTarget(firstPos);
    }

    private IEnumerator ProcessMatches(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log("=== ProcessMatches START ===");
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("ProcessMatches received null candy");
            isProcessingMove = false;
            yield break;
        }
        yield return new WaitForSeconds(0.3f);
        bool hasMatch = false;
        try
        {
            hasMatch = CheckBoard();
            Debug.Log($"CheckBoard result: {hasMatch}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in CheckBoard: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        if (hasMatch)
        {
            Debug.Log("Match found! Processing matches...");
            StartCoroutine(ProcessTurnOnMatchedBoard(true));
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            Debug.Log("No match found, swapping back");
            DoSwap(firstCandy, secondCandy);
            yield return new WaitForSeconds(0.3f);
        }
        isProcessingMove = false;
        Debug.Log("=== ProcessMatches END ===");
    }

    private bool IsAdjacent(Candy firstCandy, Candy secondCandy)
    {
        bool adjacent = Mathf.Abs(firstCandy.xIndex - secondCandy.xIndex) +
                        Mathf.Abs(firstCandy.yIndex - secondCandy.yIndex) == 1;
        Debug.Log($"IsAdjacent check: [{firstCandy.xIndex},{firstCandy.yIndex}] to [{secondCandy.xIndex},{secondCandy.yIndex}] = {adjacent}");
        return adjacent;
    }
    #endregion
}

public class MatchResult
{
    public List<Candy> connectionCandys = new List<Candy>();
    public MatchDirection direction;
}

public enum MatchDirection
{
    Vertical,
    Horizontal,
    LongVertical,
    LongHorizontal,
    Super,
    None
}