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
    private bool isInitializingBoard = false; // Flag to prevent recursive board initialization

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
        // Check for possible moves periodically if needed
        if (Input.GetKeyDown(KeyCode.M))
        {
            bool hasPossibleMatches = CheckForPossibleMatches();
            Debug.Log($"Board has possible matches: {hasPossibleMatches}");
        }
    }

    public void InitializeBoard()
    {
        if (isInitializingBoard)
        {
            Debug.LogWarning("Board initialization already in progress, skipping request");
            return;
        }

        isInitializingBoard = true;
        StartCoroutine(InitializeBoardCoroutine());
    }

    private IEnumerator InitializeBoardCoroutine()
    {
        Debug.Log("Initializing board...");

        // Fix Issue 2: Ensure complete board destruction
        ClearEntireBoard();

        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2) + 1;
        spaceingY = (float)((boardHeight - 1) / 2) - 1;

        if (candyPrefab == null || candyPrefab.Length == 0)
        {
            Debug.LogError("candyBoard: candyPrefab array is null or empty. Please assign candy prefabs in the Inspector.");
            isInitializingBoard = false;
            yield break;
        }

        for (int i = 0; i < candyPrefab.Length; i++)
        {
            if (candyPrefab[i] == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is null. Please assign a valid prefab in the Inspector.");
                isInitializingBoard = false;
                yield break;
            }
            if (candyPrefab[i].GetComponent<Candy>() == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is missing Candy component.");
                isInitializingBoard = false;
                yield break;
            }
            SpriteRenderer sr = candyPrefab[i].GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is missing SpriteRenderer component.");
                isInitializingBoard = false;
                yield break;
            }
            if (sr.sprite == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] has SpriteRenderer but no sprite assigned.");
                isInitializingBoard = false;
                yield break;
            }
        }

        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight)
        {
            Debug.LogError("candyBoard: arrayLayout is null or has incorrect row count. Please configure ArrayLayout in the Inspector.");
            isInitializingBoard = false;
            yield break;
        }
        for (int y = 0; y < boardHeight; y++)
        {
            if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth)
            {
                Debug.LogError($"candyBoard: arrayLayout.rows[{y}].row is null or has incorrect length.");
                isInitializingBoard = false;
                yield break;
            }
        }

        // Create the board ensuring no initial matches
        CreateBoardWithoutMatches();

        // Check the board after creation
        yield return new WaitForSeconds(0.5f);

        // Check for initial matches and process them
        if (CheckBoard())
        {
            Debug.Log("Initial matches found, processing...");
            StartCoroutine(ProcessTurnOnMatchedBoard(false));
            yield return new WaitForSeconds(0.5f);
        }

        // Verify if there are possible matches on the board
        if (!CheckForPossibleMatches())
        {
            Debug.Log("No possible matches on the board, reinitializing...");
            isInitializingBoard = false;
            InitializeBoard();
            yield break;
        }

        Debug.Log("Board initialization complete with valid moves available");
        isInitializingBoard = false;
    }

    private void CreateBoardWithoutMatches()
    {
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
                    // Prevent immediate matches when creating the board
                    List<int> availableTypes = GetAvailableCandyTypes(x, y);
                    if (availableTypes.Count == 0)
                    {
                        // If no valid types (shouldn't happen normally), use any type
                        availableTypes = new List<int>();
                        for (int i = 0; i < candyPrefab.Length; i++)
                        {
                            availableTypes.Add(i);
                        }
                    }

                    int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];

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

    // Get available candy types that won't create an immediate match at position (x,y)
    private List<int> GetAvailableCandyTypes(int x, int y)
    {
        List<int> availableTypes = new List<int>();
        for (int i = 0; i < candyPrefab.Length; i++)
        {
            availableTypes.Add(i);
        }

        // Check horizontal matches (left)
        if (x >= 2 &&
            candyBoard[x - 1, y] != null && candyBoard[x - 1, y].isUsable && candyBoard[x - 1, y].candy != null &&
            candyBoard[x - 2, y] != null && candyBoard[x - 2, y].isUsable && candyBoard[x - 2, y].candy != null)
        {
            CandyType type1 = candyBoard[x - 1, y].candy.GetComponent<Candy>().candyType;
            CandyType type2 = candyBoard[x - 2, y].candy.GetComponent<Candy>().candyType;

            if (type1 == type2)
            {
                int indexToRemove = (int)type1;
                if (availableTypes.Contains(indexToRemove))
                {
                    availableTypes.Remove(indexToRemove);
                }
            }
        }

        // Check vertical matches (down)
        if (y >= 2 &&
            candyBoard[x, y - 1] != null && candyBoard[x, y - 1].isUsable && candyBoard[x, y - 1].candy != null &&
            candyBoard[x, y - 2] != null && candyBoard[x, y - 2].isUsable && candyBoard[x, y - 2].candy != null)
        {
            CandyType type1 = candyBoard[x, y - 1].candy.GetComponent<Candy>().candyType;
            CandyType type2 = candyBoard[x, y - 2].candy.GetComponent<Candy>().candyType;

            if (type1 == type2)
            {
                int indexToRemove = (int)type1;
                if (availableTypes.Contains(indexToRemove))
                {
                    availableTypes.Remove(indexToRemove);
                }
            }
        }

        return availableTypes;
    }

    private void ClearEntireBoard()
    {
        // First destroy any existing candies
        if (candyToDestroy != null && candyToDestroy.Count > 0)
        {
            foreach (GameObject candy in candyToDestroy)
            {
                if (candy != null)
                {
                    Destroy(candy);
                }
            }
            candyToDestroy.Clear();
        }

        // Also check for any other candy children in the parent that might have been missed
        if (candyParent != null)
        {
            foreach (Transform child in candyParent.transform)
            {
                if (child.gameObject != null && child.gameObject != candyParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // Reset other relevant variables
        selectedCandy = null;
        isProcessingMove = false;
        candyToRemove.Clear();
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

    // Check if there are any potential matches on the board
    public bool CheckForPossibleMatches()
    {
        if (GameManager.instance.isGameOver)
        {
            return false;
        }

        // Check horizontal swaps
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth - 1; x++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x + 1, y].isUsable &&
                    candyBoard[x, y].candy != null && candyBoard[x + 1, y].candy != null)
                {
                    // Temporarily swap
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x + 1, y].candy;
                    candyBoard[x + 1, y].candy = temp;

                    // Check if this creates a match
                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x + 1, y].candy.GetComponent<Candy>();

                    int tempX1 = candy1.xIndex;
                    int tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex;
                    int tempY2 = candy2.yIndex;

                    candy1.setIndicies(x, y);
                    candy2.setIndicies(x + 1, y);

                    bool hasMatch = false;

                    // Check if the swap would create a match
                    MatchResult match1 = IsConnected(candy1);
                    MatchResult match2 = IsConnected(candy2);

                    if ((match1.connectionCandys != null && match1.connectionCandys.Count >= 3) ||
                        (match2.connectionCandys != null && match2.connectionCandys.Count >= 3))
                    {
                        hasMatch = true;
                    }

                    // Swap back
                    candyBoard[x + 1, y].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = temp;

                    // Reset indices
                    candy1.setIndicies(tempX1, tempY1);
                    candy2.setIndicies(tempX2, tempY2);

                    if (hasMatch)
                    {
                        Debug.Log($"Found potential match by swapping [{x},{y}] with [{x + 1},{y}]");
                        return true;
                    }
                }
            }
        }

        // Check vertical swaps
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight - 1; y++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x, y + 1].isUsable &&
                    candyBoard[x, y].candy != null && candyBoard[x, y + 1].candy != null)
                {
                    // Temporarily swap
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x, y + 1].candy;
                    candyBoard[x, y + 1].candy = temp;

                    // Check if this creates a match
                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x, y + 1].candy.GetComponent<Candy>();

                    int tempX1 = candy1.xIndex;
                    int tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex;
                    int tempY2 = candy2.yIndex;

                    candy1.setIndicies(x, y);
                    candy2.setIndicies(x, y + 1);

                    bool hasMatch = false;

                    // Check if the swap would create a match
                    MatchResult match1 = IsConnected(candy1);
                    MatchResult match2 = IsConnected(candy2);

                    if ((match1.connectionCandys != null && match1.connectionCandys.Count >= 3) ||
                        (match2.connectionCandys != null && match2.connectionCandys.Count >= 3))
                    {
                        hasMatch = true;
                    }

                    // Swap back
                    candyBoard[x, y + 1].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = temp;

                    // Reset indices
                    candy1.setIndicies(tempX1, tempY1);
                    candy2.setIndicies(tempX2, tempY2);

                    if (hasMatch)
                    {
                        Debug.Log($"Found potential match by swapping [{x},{y}] with [{x},{y + 1}]");
                        return true;
                    }
                }
            }
        }

        Debug.Log("No potential matches found on the board!");
        return false;
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
        if (candyToRemove.Count == 0)
        {
            yield break;
        }

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
        else
        {
            // Check if there are possible matches after cascade is complete
            yield return new WaitForSeconds(0.2f);
            if (!CheckForPossibleMatches() && !isInitializingBoard)
            {
                Debug.Log("No more possible matches after cascade, reinitializing board...");
                InitializeBoard();
            }
        }
    }

    #region Cascading Candys
    private void RemoveAndRefill(List<Candy> candyToRemove)
    {
        // First mark positions that need refilling
        HashSet<int> columnsToRefill = new HashSet<int>();

        // Remove matched candies
        foreach (Candy candy in candyToRemove)
        {
            int xIndex = candy.xIndex;
            int yIndex = candy.yIndex;

            // Add column to refill set
            columnsToRefill.Add(xIndex);

            // Destroy candy and clear board position
            Destroy(candy.gameObject);
            candyBoard[xIndex, yIndex] = new Node(true, null);
        }

        foreach (int x in columnsToRefill)
        {
            // Collapse column first - move all remaining candies down to fill gaps
            CollapseColumn(x);

            // Then fill empty spaces at the top with new candies
            FillEmptySpacesInColumn(x);
        }

        CheckBoard();
        Debug.Log("Refill complete");
    }
    private void CollapseColumn(int x)
    {
        for (int y = 0; y < boardHeight - 1; y++)
        {
            // If current position is empty and usable, look for candy above to move down
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy == null)
            {
                for (int aboveY = y + 1; aboveY < boardHeight; aboveY++)
                {
                    if (candyBoard[x, aboveY].isUsable && candyBoard[x, aboveY].candy != null)
                    {
                        // Found candy above, move it down to current position
                        Candy candyToMove = candyBoard[x, aboveY].candy.GetComponent<Candy>();
                        Vector3 targetPos = new Vector3(
                            (x - spaceingX) * spacingScale,
                            (y - spaceingY) * spacingScale,
                            candyToMove.transform.position.z
                        );

                        Debug.Log($"Moving candy from X: {x} Y: {aboveY} to X: {x} Y: {y}");
                        candyToMove.MoveToTarget(targetPos);
                        candyToMove.setIndicies(x, y);

                        // Update board references
                        candyBoard[x, y] = candyBoard[x, aboveY];
                        candyBoard[x, aboveY] = new Node(true, null);
                        break;
                    }
                }
            }
        }
    }

    // Fill empty spaces at the top of column
    private void FillEmptySpacesInColumn(int x)
    {
        for (int y = 0; y < boardHeight; y++)
        {
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy == null)
            {
                // Spawn new candy for this empty position
                List<int> availableTypes = GetAvailableCandyTypes(x, y);
                if (availableTypes.Count == 0)
                {
                    // If no valid types, use any type
                    availableTypes = new List<int>();
                    for (int i = 0; i < candyPrefab.Length; i++)
                    {
                        availableTypes.Add(i);
                    }
                }

                int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];

                Vector3 spawnPos = new Vector3(
                    (x - spaceingX) * spacingScale,
                    (boardHeight - spaceingY + y) * spacingScale, // Spawn above the visible board
                    0
                );

                Vector3 targetPos = new Vector3(
                    (x - spaceingX) * spacingScale,
                    (y - spaceingY) * spacingScale,
                    0
                );

                GameObject newCandy = Instantiate(candyPrefab[randomIndex], spawnPos, Quaternion.identity);
                newCandy.transform.SetParent(candyParent.transform);
                Candy candyComponent = newCandy.GetComponent<Candy>();
                candyComponent.setIndicies(x, y);
                candyComponent.Init(x, y, (CandyType)randomIndex);
                candyComponent.MoveToTarget(targetPos);

                candyBoard[x, y] = new Node(true, newCandy);
                candyToDestroy.Add(newCandy);

                Debug.Log($"Created new candy at column X: {x}, row Y: {y}");
            }
        }
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

            // Check if there are any valid moves after the swap back
            if (!CheckForPossibleMatches())
            {
                Debug.Log("No possible matches remain after failed swap, reinitializing board...");
                yield return new WaitForSeconds(0.5f);
                InitializeBoard();
            }
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