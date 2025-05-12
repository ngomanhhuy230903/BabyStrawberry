using System.Collections;
using System.Collections.Generic;
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
    private bool isInitializingBoard = false;
    public GameObject[] rowClearerPrefabs;
    public GameObject[] columnClearerPrefabs;

    public void Awake()
    {
        instance = this;
        ValidateSpecialPrefabs();
    }

    private void ValidateSpecialPrefabs()
    {
        if (rowClearerPrefabs == null || rowClearerPrefabs.Length != candyPrefab.Length)
        {
            Debug.LogError("rowClearerPrefabs array is null or does not match candyPrefab length.");
        }
        if (columnClearerPrefabs == null || columnClearerPrefabs.Length != candyPrefab.Length)
        {
            Debug.LogError("columnClearerPrefabs array is null or does not match candyPrefab length.");
        }
        for (int i = 0; i < candyPrefab.Length; i++)
        {
            if (rowClearerPrefabs[i] == null)
            {
                Debug.LogError($"rowClearerPrefabs[{i}] is null for candy type {(CandyType)i}.");
            }
            if (columnClearerPrefabs[i] == null)
            {
                Debug.LogError($"columnClearerPrefabs[{i}] is null for candy type {(CandyType)i}.");
            }
        }
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
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Candy"));
            if (hit.collider != null)
            {
                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
                if (candy != null && !isProcessingMove)
                {
                    Debug.Log($"Clicked candy at [{candy.xIndex},{candy.yIndex}], type: {candy.candyType}, isSpecial: {candy.isSpecial}");
                    SelectCandy(candy);
                }
                else
                {
                    Debug.LogWarning($"No Candy component on hit object: {hit.collider.gameObject.name}");
                }
            }
            else
            {
                Debug.Log("Raycast hit nothing");
            }
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            InitializeBoard();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.Log($"Current selectedCandy: {(selectedCandy == null ? "null" : $"{selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]")}");
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log($"Board has possible matches: {CheckForPossibleMatches()}");
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
        selectedCandy = null;
        ClearEntireBoard();

        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2) + 1;
        spaceingY = (float)((boardHeight - 1) / 2) - 1;

        if (candyPrefab == null || candyPrefab.Length == 0)
        {
            Debug.LogError("candyPrefab array is null or empty.");
            isInitializingBoard = false;
            yield break;
        }

        for (int i = 0; i < candyPrefab.Length; i++)
        {
            if (candyPrefab[i] == null)
            {
                Debug.LogError($"candyPrefab[{i}] is null.");
                isInitializingBoard = false;
                yield break;
            }
            if (candyPrefab[i].GetComponent<Candy>() == null)
            {
                Debug.LogError($"candyPrefab[{i}] is missing Candy component.");
                isInitializingBoard = false;
                yield break;
            }
            SpriteRenderer sr = candyPrefab[i].GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"candyPrefab[{i}] is missing SpriteRenderer component.");
                isInitializingBoard = false;
                yield break;
            }
            if (sr.sprite == null)
            {
                Debug.LogError($"candyPrefab[{i}] has SpriteRenderer but no sprite assigned.");
                isInitializingBoard = false;
                yield break;
            }
        }

        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight)
        {
            Debug.LogError("arrayLayout is null or has incorrect row count.");
            isInitializingBoard = false;
            yield break;
        }
        for (int y = 0; y < boardHeight; y++)
        {
            if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth)
            {
                Debug.LogError($"arrayLayout.rows[{y}].row is null or has incorrect length.");
                isInitializingBoard = false;
                yield break;
            }
        }

        CreateBoardWithoutMatches();
        yield return new WaitForSeconds(0.5f);

        if (CheckBoard())
        {
            Debug.Log("Initial matches found, processing...");
            StartCoroutine(ProcessTurnOnMatchedBoard(false));
            yield return new WaitForSeconds(0.5f);
        }

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
                    List<int> availableTypes = GetAvailableCandyTypes(x, y);
                    if (availableTypes.Count == 0)
                    {
                        availableTypes = new List<int>();
                        for (int i = 0; i < candyPrefab.Length; i++)
                        {
                            availableTypes.Add(i);
                        }
                    }

                    int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];
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
                        Debug.LogError($"Candy at [{x},{y}] is missing Candy component.");
                        Destroy(candy);
                        continue;
                    }
                    candyComponent.setIndicies(x, y);
                    candyComponent.Init(x, y, (CandyType)randomIndex);
                    candyBoard[x, y] = new Node(true, candy);
                    candyToDestroy.Add(candy);
                    Debug.Log($"Created candy at [{x},{y}] with type {candyComponent.candyType}, indices [{candyComponent.xIndex},{candyComponent.yIndex}]");
                }
            }
        }
    }

    private List<int> GetAvailableCandyTypes(int x, int y)
    {
        List<int> availableTypes = new List<int>();
        for (int i = 0; i < candyPrefab.Length; i++)
        {
            availableTypes.Add(i);
        }

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
        Debug.Log($"ClearEntireBoard: selectedCandy = {(selectedCandy == null ? "null" : $"[{selectedCandy.xIndex},{selectedCandy.yIndex}]")}");
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

        if (candyParent != null)
        {
            foreach (Transform child in candyParent.transform)
            {
                if (child.gameObject != null && child.gameObject != candyParent)
                {
                    Debug.Log($"Destroying candy: {child.gameObject.name} at position {child.position}");
                    Destroy(child.gameObject);
                }
            }
        }

        selectedCandy = null;
        isProcessingMove = false;
        candyToRemove.Clear();
        Debug.Log("ClearEntireBoard: selectedCandy set to null");
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

    public bool CheckForPossibleMatches()
    {
        if (GameManager.instance.isGameOver)
        {
            return false;
        }

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth - 1; x++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x + 1, y].isUsable &&
                    candyBoard[x, y].candy != null && candyBoard[x + 1, y].candy != null)
                {
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x + 1, y].candy;
                    candyBoard[x + 1, y].candy = temp;

                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x + 1, y].candy.GetComponent<Candy>();

                    int tempX1 = candy1.xIndex;
                    int tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex;
                    int tempY2 = candy2.yIndex;

                    candy1.setIndicies(x, y);
                    candy2.setIndicies(x + 1, y);

                    bool hasMatch = false;
                    MatchResult match1 = IsConnected(candy1);
                    MatchResult match2 = IsConnected(candy2);

                    if ((match1.connectionCandys != null && match1.connectionCandys.Count >= 3) ||
                        (match2.connectionCandys != null && match2.connectionCandys.Count >= 3))
                    {
                        hasMatch = true;
                    }

                    candyBoard[x + 1, y].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = temp;

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

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight - 1; y++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x, y + 1].isUsable &&
                    candyBoard[x, y].candy != null && candyBoard[x, y + 1].candy != null)
                {
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x, y + 1].candy;
                    candyBoard[x, y + 1].candy = temp;

                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x, y + 1].candy.GetComponent<Candy>();

                    int tempX1 = candy1.xIndex;
                    int tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex;
                    int tempY2 = candy2.yIndex;

                    candy1.setIndicies(x, y);
                    candy2.setIndicies(x, y + 1);

                    bool hasMatch = false;
                    MatchResult match1 = IsConnected(candy1);
                    MatchResult match2 = IsConnected(candy2);

                    if ((match1.connectionCandys != null && match1.connectionCandys.Count >= 3) ||
                        (match2.connectionCandys != null && match2.connectionCandys.Count >= 3))
                    {
                        hasMatch = true;
                    }

                    candyBoard[x, y + 1].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = temp;

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
        if (this.candyToRemove.Count == 0) // Use this.candyToRemove which is populated by CheckBoard
        {
            isProcessingMove = false; // Ensure this is reset if no candies to remove
            yield break;
        }

        // Create a copy of the initial matches for processing special candy creation logic
        List<Candy> initialMatches = new List<Candy>(this.candyToRemove);

        // RemoveAndRefill will now return all candies that were actually destroyed (initial + special effects)
        HashSet<Candy> allDestroyedThisTurn = RemoveAndRefill(initialMatches);

        if (allDestroyedThisTurn.Count > 0)
        {
            GameManager.instance.ProcessTurn(allDestroyedThisTurn.Count, subtractMoves);
        }

        // Clear the board's main list after processing
        this.candyToRemove.Clear();

        yield return new WaitForSeconds(0.4f); // Delay for animations and effects

        if (CheckBoard()) // Check for new matches caused by refill
        {
            StartCoroutine(ProcessTurnOnMatchedBoard(false)); // Process cascades without subtracting moves
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
            if (!CheckForPossibleMatches() && !isInitializingBoard)
            {
                Debug.Log("No more possible matches after cascade, reinitializing board...");
                InitializeBoard();
            }
            isProcessingMove = false; // Processing finished
            selectedCandy = null;
        }
    }

    // Modified RemoveAndRefill to handle chained special activations and return all destroyed candies
    private HashSet<Candy> RemoveAndRefill(List<Candy> initialMatches)
    {
        HashSet<Candy> allCandiesToDestroySet = new HashSet<Candy>();
        Queue<Candy> processQueue = new Queue<Candy>();
        List<Candy> specialCandiesActivatedThisCycle = new List<Candy>(); // To prevent re-activation in the same cycle

        // Add initial matches to the queue and destruction set
        foreach (Candy candy in initialMatches)
        {
            if (allCandiesToDestroySet.Add(candy)) // .Add returns true if the item was new to the set
            {
                processQueue.Enqueue(candy);
                candy.isMatched = true; // Mark as matched
            }
        }

        while (processQueue.Count > 0)
        {
            Candy currentCandy = processQueue.Dequeue();

            // Check if this candy is special and should trigger its effect
            // It should trigger if:
            // 1. It's special.
            // 2. It's marked for destruction (i.e., it was part of a match or caught in another special's effect).
            // 3. It hasn't already activated in this specific RemoveAndRefill cycle.
            if (currentCandy.isSpecial && allCandiesToDestroySet.Contains(currentCandy) && !specialCandiesActivatedThisCycle.Contains(currentCandy))
            {
                Debug.Log($"Processing special effect for {currentCandy.candyType} at [{currentCandy.xIndex},{currentCandy.yIndex}]");
                specialCandiesActivatedThisCycle.Add(currentCandy);
                currentCandy.ActivateSpecialEffectAndPlayVisuals(); // Play visuals like flash and beams

                List<Candy> newlyAffectedBySpecial = new List<Candy>();
                if (currentCandy.specialEffect == SpecialCandyEffect.ClearRow)
                {
                    for (int x = 0; x < boardWidth; x++)
                    {
                        if (candyBoard[x, currentCandy.yIndex].isUsable && candyBoard[x, currentCandy.yIndex].candy != null)
                        {
                            Candy affectedCandy = candyBoard[x, currentCandy.yIndex].candy.GetComponent<Candy>();
                            if (affectedCandy != null && allCandiesToDestroySet.Add(affectedCandy))
                            {
                                newlyAffectedBySpecial.Add(affectedCandy);
                            }
                        }
                    }
                }
                else if (currentCandy.specialEffect == SpecialCandyEffect.ClearColumn)
                {
                    for (int y = 0; y < boardHeight; y++)
                    {
                        if (candyBoard[currentCandy.xIndex, y].isUsable && candyBoard[currentCandy.xIndex, y].candy != null)
                        {
                            Candy affectedCandy = candyBoard[currentCandy.xIndex, y].candy.GetComponent<Candy>();
                            if (affectedCandy != null && allCandiesToDestroySet.Add(affectedCandy))
                            {
                                newlyAffectedBySpecial.Add(affectedCandy);
                            }
                        }
                    }
                }

                // If these newly affected candies are also special, add them to the queue to process their effects
                foreach (Candy newlyHitCandy in newlyAffectedBySpecial)
                {
                    newlyHitCandy.isMatched = true; // Mark for destruction
                    if (newlyHitCandy.isSpecial && !processQueue.Contains(newlyHitCandy) && !specialCandiesActivatedThisCycle.Contains(newlyHitCandy))
                    {
                        processQueue.Enqueue(newlyHitCandy);
                    }
                }
            }
        }

        // Create new special candies based on the original matches (e.g., 4-in-a-row)
        // This should use the 'initialMatches' list, not 'allCandiesToDestroySet',
        // to ensure special candies are formed from direct matches, not explosions.
        CreateSpecialCandyIfMatch(initialMatches);

        HashSet<int> columnsToRefill = new HashSet<int>();
        if (allCandiesToDestroySet.Count > 0)
        {
            foreach (Candy candy in allCandiesToDestroySet)
            {
                int xIndex = candy.xIndex;
                int yIndex = candy.yIndex;
                columnsToRefill.Add(xIndex);

                // Ensure the candy being destroyed is the one on the board at its coordinates,
                // or handle cases where it might have been replaced (e.g., by CreateSpecialCandyIfMatch)
                if (candyBoard[xIndex, yIndex].candy == candy.gameObject)
                {
                    Destroy(candy.gameObject);
                    candyBoard[xIndex, yIndex] = new Node(true, null);
                }
                else if (candy.gameObject != null) // If it's not on the board but exists and is in the set, destroy it.
                {
                    Destroy(candy.gameObject);
                    // If the node it was supposed to be in still has something else, don't nullify node unless sure.
                    // This path implies the candy object was slated for destruction but might have been moved or board changed.
                    // Most importantly, the candy object in allCandiesToDestroySet is destroyed.
                }
            }
        }

        // Collapse columns and fill empty spaces
        foreach (int x in columnsToRefill)
        {
            CollapseColumn(x);
            FillEmptySpacesInColumn(x);
        }

        Debug.Log($"Refill complete. Total destroyed: {allCandiesToDestroySet.Count}");
        return allCandiesToDestroySet;
    }


    private void CreateSpecialCandyIfMatch(List<Candy> matchedCandies)
    {
        if (matchedCandies == null || matchedCandies.Count < 4)
            return;

        bool isHorizontalMatch = true;
        int firstY = matchedCandies[0].yIndex;
        foreach (Candy candy in matchedCandies)
        {
            if (candy.yIndex != firstY)
            {
                isHorizontalMatch = false;
                break;
            }
        }

        bool isVerticalMatch = true;
        int firstX = matchedCandies[0].xIndex;
        foreach (Candy candy in matchedCandies)
        {
            if (candy.xIndex != firstX)
            {
                isVerticalMatch = false;
                break;
            }
        }

        if (!isHorizontalMatch && !isVerticalMatch)
            return;

        Candy centerCandy = matchedCandies[matchedCandies.Count / 2];
        int specialX = centerCandy.xIndex;
        int specialY = centerCandy.yIndex;
        CandyType originalType = centerCandy.candyType;

        matchedCandies.Remove(centerCandy);

        if (isHorizontalMatch)
        {
            CreateRowClearerCandy(specialX, specialY, originalType);
        }
        else if (isVerticalMatch)
        {
            CreateColumnClearerCandy(specialX, specialY, originalType);
        }
    }

    private void CreateRowClearerCandy(int x, int y, CandyType originalType)
    {
        GameObject specialCandyPrefab = GetSpecialCandyPrefab(originalType, true);
        if (specialCandyPrefab == null)
        {
            Debug.LogError($"No special candy prefab found for type {originalType} (row clearer)");
            return;
        }

        Vector3 position = new Vector3(
            (x - spaceingX) * spacingScale,
            (y - spaceingY) * spacingScale,
            0
        );

        GameObject specialCandy = Instantiate(specialCandyPrefab, position, Quaternion.identity);
        specialCandy.transform.SetParent(candyParent.transform);

        Candy candyComponent = specialCandy.GetComponent<Candy>();
        candyComponent.setIndicies(x, y);
        candyComponent.Init(x, y, originalType, true, SpecialCandyEffect.ClearRow);

        if (candyBoard[x, y].candy != null)
        {
            Destroy(candyBoard[x, y].candy);
        }
        candyBoard[x, y] = new Node(true, specialCandy);
        candyToDestroy.Add(specialCandy);

        Debug.Log($"Created LongHorizontal candy at [{x},{y}] with type {originalType}");
    }

    private void CreateColumnClearerCandy(int x, int y, CandyType originalType)
    {
        GameObject specialCandyPrefab = GetSpecialCandyPrefab(originalType, false);
        if (specialCandyPrefab == null)
        {
            Debug.LogError($"No special candy prefab found for type {originalType} (column clearer)");
            return;
        }

        Vector3 position = new Vector3(
            (x - spaceingX) * spacingScale,
            (y - spaceingY) * spacingScale,
            0
        );

        GameObject specialCandy = Instantiate(specialCandyPrefab, position, Quaternion.identity);
        specialCandy.transform.SetParent(candyParent.transform);

        Candy candyComponent = specialCandy.GetComponent<Candy>();
        candyComponent.setIndicies(x, y);
        candyComponent.Init(x, y, originalType, true, SpecialCandyEffect.ClearColumn);

        if (candyBoard[x, y].candy != null)
        {
            Destroy(candyBoard[x, y].candy);
        }
        candyBoard[x, y] = new Node(true, specialCandy);
        candyToDestroy.Add(specialCandy);

        Debug.Log($"Created LongVertical candy at [{x},{y}] with type {originalType}");
    }

    private GameObject GetSpecialCandyPrefab(CandyType type, bool isRowClearer)
    {
        if (isRowClearer)
        {
            if (rowClearerPrefabs != null && rowClearerPrefabs.Length > (int)type && rowClearerPrefabs[(int)type] != null)
            {
                return rowClearerPrefabs[(int)type];
            }
        }
        else
        {
            if (columnClearerPrefabs != null && columnClearerPrefabs.Length > (int)type && columnClearerPrefabs[(int)type] != null)
            {
                return columnClearerPrefabs[(int)type];
            }
        }

        Debug.LogError($"No special candy prefab found for {type}!");
        return null;
    }

    private void CollapseColumn(int x)
    {
        for (int y = 0; y < boardHeight - 1; y++)
        {
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy == null)
            {
                for (int aboveY = y + 1; aboveY < boardHeight; aboveY++)
                {
                    if (candyBoard[x, aboveY].isUsable && candyBoard[x, aboveY].candy != null)
                    {
                        Candy candyToMove = candyBoard[x, aboveY].candy.GetComponent<Candy>();
                        Vector3 targetPos = new Vector3(
                            (x - spaceingX) * spacingScale,
                            (y - spaceingY) * spacingScale,
                            candyToMove.transform.position.z
                        );

                        Debug.Log($"Moving candy from X: {x} Y: {aboveY} to X: {x} Y: {y}");
                        candyToMove.MoveToTarget(targetPos);
                        candyToMove.setIndicies(x, y);

                        candyBoard[x, y] = candyBoard[x, aboveY];
                        candyBoard[x, aboveY] = new Node(true, null);
                        break;
                    }
                }
            }
        }
    }

    private void FillEmptySpacesInColumn(int x)
    {
        for (int y = 0; y < boardHeight; y++)
        {
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy == null)
            {
                List<int> availableTypes = GetAvailableCandyTypes(x, y);
                if (availableTypes.Count == 0)
                {
                    availableTypes = new List<int>();
                    for (int i = 0; i < candyPrefab.Length; i++)
                    {
                        availableTypes.Add(i);
                    }
                }

                int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];

                Vector3 spawnPos = new Vector3(
                    (x - spaceingX) * spacingScale,
                    (boardHeight - spaceingY + y) * spacingScale,
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
                    Debug.Log("Super horizontal match, color: " + matchCandy.connectionCandys[0].candyType);
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
                    Debug.Log("Super vertical match, color: " + matchCandy.connectionCandys[0].candyType);
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
            Debug.Log("Normal horizontal match, color: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.Horizontal };
        }
        else if (connectionCandys.Count >= 4)
        {
            Debug.Log("Long horizontal match, color: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.LongHorizontal };
        }
        connectionCandys.Clear();
        connectionCandys.Add(candy);
        CheckDirection(candy, Vector2Int.up, connectionCandys);
        CheckDirection(candy, Vector2Int.down, connectionCandys);
        if (connectionCandys.Count == 3)
        {
            Debug.Log("Normal vertical match, color: " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.Vertical };
        }
        else if (connectionCandys.Count >= 4)
        {
            Debug.Log("Long vertical match, color: " + connectionCandys[0].candyType);
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

    public void ClearRow(int rowIndex)
    {
        List<Candy> candiesToRemove = new List<Candy>();
        for (int x = 0; x < boardWidth; x++)
        {
            if (candyBoard[x, rowIndex].isUsable && candyBoard[x, rowIndex].candy != null)
            {
                Candy candy = candyBoard[x, rowIndex].candy.GetComponent<Candy>();
                if (candy != null)
                {
                    candiesToRemove.Add(candy);
                }
            }
        }

        if (candiesToRemove.Count > 0)
        {
            RemoveAndRefill(candiesToRemove);
            GameManager.instance.ProcessTurn(candiesToRemove.Count, true);
        }
    }

    public void ClearColumn(int columnIndex)
    {
        List<Candy> candiesToRemove = new List<Candy>();
        for (int y = 0; y < boardHeight; y++)
        {
            if (candyBoard[columnIndex, y].isUsable && candyBoard[columnIndex, y].candy != null)
            {
                Candy candy = candyBoard[columnIndex, y].candy.GetComponent<Candy>();
                if (candy != null)
                {
                    candiesToRemove.Add(candy);
                }
            }
        }

        if (candiesToRemove.Count > 0)
        {
            RemoveAndRefill(candiesToRemove);
            GameManager.instance.ProcessTurn(candiesToRemove.Count, true);
        }
    }

    public void SelectCandy(Candy candy)
    {
        if (candy == null)
        {
            Debug.LogError("Cannot select null candy");
            return;
        }

        Debug.Log($"SelectCandy: Input candy = {candy.candyType} at [{candy.xIndex},{candy.yIndex}], isSpecial: {candy.isSpecial}");
        Debug.Log($"SelectCandy: Current selectedCandy = {(selectedCandy == null ? "null" : $"{selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]")}");

        if (isProcessingMove)
        {
            Debug.Log("Still processing previous move, cannot select");
            return;
        }

        // Validate candy indices
        if (candy.xIndex < 0 || candy.xIndex >= boardWidth || candy.yIndex < 0 || candy.yIndex >= boardHeight)
        {
            Debug.LogError($"Invalid indices for candy: [{candy.xIndex},{candy.yIndex}]");
            return;
        }

        if (selectedCandy == null)
        {
            selectedCandy = candy;
            selectedCandy.SetSelected(true);
            Debug.Log($"Selected first candy: {selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]");
        }
        else if (selectedCandy == candy)
        {
            selectedCandy.SetSelected(false);
            selectedCandy = null;
            Debug.Log("Deselected candy");
        }
        else
        {
            Debug.Log($"Selected second candy: {candy.candyType} at [{candy.xIndex},{candy.yIndex}], attempting swap");
            Candy firstCandy = selectedCandy;
            Candy secondCandy = candy;
            firstCandy.SetSelected(false);
            selectedCandy = null;
            SwapCandy(firstCandy, secondCandy);
        }
    }

    private void SwapCandy(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log($"SwapCandy: Attempting to swap {firstCandy.candyType}[{firstCandy.xIndex},{firstCandy.yIndex}] with {secondCandy.candyType}[{secondCandy.xIndex},{secondCandy.yIndex}]");

        // Validate candies
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("Cannot swap with null candy");
            isProcessingMove = false;
            return;
        }

        // Validate indices
        if (firstCandy.xIndex < 0 || firstCandy.xIndex >= boardWidth || firstCandy.yIndex < 0 || firstCandy.yIndex >= boardHeight ||
            secondCandy.xIndex < 0 || secondCandy.xIndex >= boardWidth || secondCandy.yIndex < 0 || secondCandy.yIndex >= boardHeight)
        {
            Debug.LogError($"Invalid indices: firstCandy [{firstCandy.xIndex},{firstCandy.yIndex}], secondCandy [{secondCandy.xIndex},{secondCandy.yIndex}]");
            isProcessingMove = false;
            return;
        }

        if (!IsAdjacent(firstCandy, secondCandy))
        {
            Debug.LogWarning($"Candies are not adjacent: [{firstCandy.xIndex},{firstCandy.yIndex}] and [{secondCandy.xIndex},{secondCandy.yIndex}]");
            isProcessingMove = false;
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

        yield return new WaitForSeconds(0.3f); // Wait for swap animation
        bool hasMatch = false;

        try
        {
            hasMatch = CheckBoard(); // CheckBoard populates this.candyToRemove
            Debug.Log($"CheckBoard result: {hasMatch}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in CheckBoard: {e.Message}\nStackTrace: {e.StackTrace}");
            isProcessingMove = false; // Reset on error
            yield break;
        }

        if (hasMatch)
        {
            Debug.Log("Match found! Processing matches...");
            // this.candyToRemove is already populated by CheckBoard.
            // ProcessTurnOnMatchedBoard will use it.
            StartCoroutine(ProcessTurnOnMatchedBoard(true));
            // isProcessingMove and selectedCandy will be reset at the end of ProcessTurnOnMatchedBoard's cascade.
        }
        else // No general match found by CheckBoard after the swap.
             // Check if the swapped candies themselves (if special) created a specific match.
        {
            bool specialActivatedBySwap = false;
            List<Candy> tempMatchListForSpecial = new List<Candy>();

            if (firstCandy.isSpecial)
            {
                MatchResult firstCandyConnection = IsConnected(firstCandy);
                if (firstCandyConnection.connectionCandys != null && firstCandyConnection.connectionCandys.Count >= 3)
                {
                    Debug.Log($"Special {firstCandy.candyType} at [{firstCandy.xIndex},{firstCandy.yIndex}] formed a match of {firstCandyConnection.connectionCandys.Count} by swapping.");
                    foreach (Candy c in firstCandyConnection.connectionCandys)
                    {
                        if (!this.candyToRemove.Contains(c)) this.candyToRemove.Add(c); // Use the board's list
                    }
                    specialActivatedBySwap = true;
                }
            }

            if (secondCandy.isSpecial)
            {
                MatchResult secondCandyConnection = IsConnected(secondCandy);
                if (secondCandyConnection.connectionCandys != null && secondCandyConnection.connectionCandys.Count >= 3)
                {
                    Debug.Log($"Special {secondCandy.candyType} at [{secondCandy.xIndex},{secondCandy.yIndex}] formed a match of {secondCandyConnection.connectionCandys.Count} by swapping.");
                    foreach (Candy c in secondCandyConnection.connectionCandys)
                    {
                        if (!this.candyToRemove.Contains(c)) this.candyToRemove.Add(c); // Use the board's list
                    }
                    specialActivatedBySwap = true;
                }
            }

            if (specialActivatedBySwap)
            {
                Debug.Log("Special candy activated by direct swap match. Processing turn.");
                StartCoroutine(ProcessTurnOnMatchedBoard(true));
                // isProcessingMove and selectedCandy will be reset at the end of ProcessTurnOnMatchedBoard's cascade.
            }
            else
            {
                Debug.Log("No match found from swap (neither general nor specific special activation), swapping back.");
                DoSwap(firstCandy, secondCandy); // Swap back
                yield return new WaitForSeconds(0.3f); // Animation for swap back

                isProcessingMove = false; // Crucial: reset after swap back and no match
                selectedCandy = null;

                if (!CheckForPossibleMatches()) // Check for general board state
                {
                    Debug.Log("No possible matches remain after failed swap, reinitializing board...");
                    yield return new WaitForSeconds(0.5f); // Give time for user to see failed swap
                    InitializeBoard(); // This will set isProcessingMove internally if it runs
                }
            }
        }
        // isProcessingMove is generally set to false at the end of successful ProcessTurnOnMatchedBoard cascades
        // or explicitly in the swap-back case.
        Debug.Log("=== ProcessMatches END (or handing over to ProcessTurnOnMatchedBoard) ===");
    }

    private bool IsAdjacent(Candy firstCandy, Candy secondCandy)
    {
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("IsAdjacent: One or both candies are null");
            return false;
        }

        bool adjacent = Mathf.Abs(firstCandy.xIndex - secondCandy.xIndex) +
                        Mathf.Abs(firstCandy.yIndex - secondCandy.yIndex) == 1;
        Debug.Log($"IsAdjacent check: [{firstCandy.xIndex},{firstCandy.yIndex}] to [{secondCandy.xIndex},{secondCandy.yIndex}] = {adjacent}");
        return adjacent;
    }
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