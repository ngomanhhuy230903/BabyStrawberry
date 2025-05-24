using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CandyBoard : MonoBehaviour
{
    public int boardWidth = 6;
    public int boardHeight = 8;
    public float spaceingX;
    public float spaceingY;
    public float spacingScale = 1.5f;
    public GameObject[] candyPrefabs;
    public GameObject[] rowClearerPrefabs;
    public GameObject[] columnClearerPrefabs;
    public Node[,] candyBoard;
    public GameObject candyParent;
    [SerializeField] List<Candy> candyToRemove = new List<Candy>();

    [SerializeField] private Candy _selectedCandy; // Giữ lại để các state có thể truy cập nếu cần

    private IBoardState currentState; // Trạng thái hiện tại của board
    public ArrayLayout arrayLayout;
    public static CandyBoard instance;
    private CandyFactory _candyFactory;

    [Header("Pooling Settings")]
    public int initialPoolSizePerType = 30;

    public void Awake()
    {
        instance = this;
        ValidateSpecialPrefabs();
        if (candyParent == null) Debug.LogError("CandyBoard Critical Error: candyParent is not assigned in Inspector!");
        if (candyPrefabs == null || candyPrefabs.Length == 0) Debug.LogError("CandyBoard Critical Error: candyPrefabs array is not assigned or empty in Inspector!");

        _candyFactory = new CandyFactory(this.candyPrefabs, this.rowClearerPrefabs, this.columnClearerPrefabs, this.candyParent.transform, initialPoolSizePerType);
    }

    public void Start()
    {
        if (_candyFactory == null)
        {
            Debug.LogError("CandyFactory was not initialized in Awake. Aborting Start.");
            return;
        }
        SetState(new InitializingBoardState(this));
    }

    public void SetState(IBoardState newState)
    {
        currentState?.OnExit();
        currentState = newState;
        if (currentState != null)
        {
            currentState.OnEnter();
        }
        else
        {
            Debug.LogError("SetState called with null newState!");
        }
    }

    private void ValidateSpecialPrefabs()
    {
        if (rowClearerPrefabs == null || rowClearerPrefabs.Length != candyPrefabs.Length)
        {
            Debug.LogError("rowClearerPrefabs array is null or does not match candyPrefabs length.");
        }
        if (columnClearerPrefabs == null || columnClearerPrefabs.Length != candyPrefabs.Length)
        {
            Debug.LogError("columnClearerPrefabs array is null or does not match candyPrefabs length.");
        }
        for (int i = 0; i < candyPrefabs.Length; i++)
        {
            if (rowClearerPrefabs[i] == null) Debug.LogError($"rowClearerPrefabs[{i}] is null for candy type {(CandyType)i}.");
            if (columnClearerPrefabs[i] == null) Debug.LogError($"columnClearerPrefabs[{i}] is null for candy type {(CandyType)i}.");
        }
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
                if (candy != null && currentState != null)
                {
                    currentState.HandleCandyClick(candy);
                }
            }
        }

        // --- Debug Keys ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Spacebar pressed: Re-initializing board via new InitializingBoardState.");
            SetState(new InitializingBoardState(this));
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.Log($"Current selectedCandy: {(_selectedCandy == null ? "null" : $"{_selectedCandy.candyType} at [{_selectedCandy.xIndex},{_selectedCandy.yIndex}]")}");
            Debug.Log($"Current Board State: {(currentState == null ? "null" : currentState.GetType().Name)}");
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log($"Board has possible matches: {CheckForPossibleMatches()}");
        }

        currentState?.UpdateState();
    }

    public void SetSelectedCandy(Candy candy)
    {
        _selectedCandy = candy;
    }

    public Candy GetSelectedCandy()
    {
        return _selectedCandy;
    }

    public void DeselectCurrentCandy()
    {
        if (_selectedCandy != null)
        {
            _selectedCandy.SetSelected(false);
            _selectedCandy = null;
        }
    }

    public IEnumerator InitializeBoardCoroutineInternal()
    {
        Debug.Log("Initializing board (Internal Coroutine)...");
        DeselectCurrentCandy();
        ClearEntireBoard();

        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2) + 1;
        spaceingY = (float)((boardHeight - 1) / 2) - 1;

        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight) { Debug.LogError("arrayLayout is null or has incorrect row count."); yield break; }
        for (int y = 0; y < boardHeight; y++) { if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth) { Debug.LogError($"arrayLayout.rows[{y}].row is null or has incorrect length."); yield break; } }

        CreateBoardWithoutMatches();
        yield return new WaitForSeconds(0.1f);

        bool initialMatchesFound = CheckBoard();
        if (initialMatchesFound)
        {
            Debug.Log("Initial matches found, processing via ProcessTurnOnMatchedBoard (no move subtract)...");
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(false));
        }
        FinalizeBoardInitialization();
    }

    private void FinalizeBoardInitialization()
    {
        if (!CheckForPossibleMatches())
        {
            Debug.Log("No possible matches on the board after init/initial processing, re-triggering initialization...");
            SetState(new InitializingBoardState(this));
        }
        else
        {
            Debug.Log("Board initialization complete with valid moves available.");
            SetState(new IdleState(this));
        }
    }

    public IEnumerator ProcessSwapAndMatchesCoroutine(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log($"=== ProcessSwapAndMatchesCoroutine START: {firstCandy.name} with {secondCandy.name} ===");
        DoSwap(firstCandy, secondCandy);
        yield return new WaitForSeconds(0.3f);

        bool hasMatch = CheckBoard();
        Debug.Log($"CheckBoard result after swap: {hasMatch}");

        if (hasMatch)
        {
            Debug.Log("Match found after swap! Processing matches via ProcessTurnOnMatchedBoard...");
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(true));
        }
        else
        {
            Debug.Log("No match found from swap, swapping back.");
            DoSwap(firstCandy, secondCandy);
            yield return new WaitForSeconds(0.3f);
            FinalizeCurrentTurnProcessing(); // No match, so finalize immediately.
        }
        Debug.Log("=== ProcessSwapAndMatchesCoroutine END ===");
    }

    public IEnumerator ProcessTurnOnMatchedBoard(bool subtractMoves)
    {
        if (this.candyToRemove.Count == 0)
        {
            FinalizeCurrentTurnProcessing();
            yield break;
        }

        List<Candy> initialMatches = new List<Candy>(this.candyToRemove);
        HashSet<Candy> allDestroyedThisTurn = RemoveAndRefill(initialMatches);

        if (allDestroyedThisTurn.Count > 0 && GameManager.instance != null)
        {
            GameManager.instance.ProcessTurn(allDestroyedThisTurn.Count, subtractMoves);
        }

        this.candyToRemove.Clear();
        yield return new WaitForSeconds(0.4f);

        if (CheckBoard())
        {
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(false)); // Cascade
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
            FinalizeCurrentTurnProcessing();
        }
    }

    public void FinalizeCurrentTurnProcessing()
    {
        Debug.Log("Finalizing current turn processing.");
        DeselectCurrentCandy();
        if (!CheckForPossibleMatches() && !(currentState is InitializingBoardState))
        {
            Debug.Log("No more possible matches after turn, setting NoPossibleMovesState.");
            SetState(new NoPossibleMovesState(this));
        }
        else if (!(currentState is InitializingBoardState))
        {
            Debug.Log("Turn processing complete, moves available, setting IdleState.");
            SetState(new IdleState(this));
        }
    }

    public IEnumerator HandleNoPossibleMovesCoroutine()
    {
        Debug.Log("HandleNoPossibleMovesCoroutine: No possible moves. Re-initializing board after delay.");
        yield return new WaitForSeconds(1.5f);
        SetState(new InitializingBoardState(this));
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
                        for (int i = 0; i < this.candyPrefabs.Length; i++) availableTypes.Add(i);
                    }
                    int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];
                    CandyType typeToCreate = (CandyType)randomIndex;

                    Candy newCandy = _candyFactory.CreateRegularCandy(typeToCreate, x, y, position);
                    if (newCandy != null)
                    {
                        candyBoard[x, y] = new Node(true, newCandy.gameObject);
                    }
                    else
                    {
                        Debug.LogError($"Failed to create candy via factory at [{x},{y}].");
                        candyBoard[x, y] = new Node(true, null);
                    }
                }
            }
        }
    }

    private List<int> GetAvailableCandyTypes(int x, int y)
    {
        List<int> availableTypes = new List<int>();
        for (int i = 0; i < candyPrefabs.Length; i++) availableTypes.Add(i);

        if (x >= 2 &&
            candyBoard[x - 1, y]?.isUsable == true && candyBoard[x - 1, y]?.candy != null &&
            candyBoard[x - 2, y]?.isUsable == true && candyBoard[x - 2, y]?.candy != null)
        {
            CandyType type1 = candyBoard[x - 1, y].candy.GetComponent<Candy>().candyType;
            CandyType type2 = candyBoard[x - 2, y].candy.GetComponent<Candy>().candyType;
            if (type1 == type2) availableTypes.Remove((int)type1);
        }

        if (y >= 2 &&
            candyBoard[x, y - 1]?.isUsable == true && candyBoard[x, y - 1]?.candy != null &&
            candyBoard[x, y - 2]?.isUsable == true && candyBoard[x, y - 2]?.candy != null)
        {
            CandyType type1 = candyBoard[x, y - 1].candy.GetComponent<Candy>().candyType;
            CandyType type2 = candyBoard[x, y - 2].candy.GetComponent<Candy>().candyType;
            if (type1 == type2) availableTypes.Remove((int)type1);
        }
        return availableTypes;
    }

    public void ClearEntireBoard()
    {
        Debug.Log("ClearEntireBoard: Initiating board clear.");

        if (_candyFactory != null && candyBoard != null)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    if (candyBoard[x, y]?.isUsable == true && candyBoard[x, y]?.candy != null)
                    {
                        Candy candyComponent = candyBoard[x, y].candy.GetComponent<Candy>();
                        if (candyComponent != null)
                        {
                            _candyFactory.ReturnCandyToPool(candyComponent);
                        }
                        else
                        {
                            Destroy(candyBoard[x, y].candy);
                        }
                        candyBoard[x, y].candy = null;
                    }
                }
            }
        }
        else if (_candyFactory == null) // Fallback
        {
            Debug.LogError("ClearEntireBoard: _candyFactory is null! Falling back to destroying children.");
            if (candyParent != null)
            {
                foreach (Transform child in candyParent.transform) Destroy(child.gameObject);
            }
        }

        candyToRemove?.Clear();
        DeselectCurrentCandy();
        Debug.Log("ClearEntireBoard: Board cleared and candies returned to pool.");
    }

    public bool CheckForPossibleMatches()
    {
        if (GameManager.instance.isGameOver) return false;

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth - 1; x++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x + 1, y].isUsable && candyBoard[x, y].candy != null && candyBoard[x + 1, y].candy != null)
                {
                    // Tạm hoán đổi để kiểm tra
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x + 1, y].candy;
                    candyBoard[x + 1, y].candy = temp;

                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x + 1, y].candy.GetComponent<Candy>();
                    int tempX1 = candy1.xIndex, tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex, tempY2 = candy2.yIndex;
                    candy1.setIndicies(x, y); candy2.setIndicies(x + 1, y);

                    // Kiểm tra match
                    bool hasMatch = (IsConnected(candy1).connectionCandys.Count >= 3) || (IsConnected(candy2).connectionCandys.Count >= 3);

                    // Hoán đổi lại
                    candyBoard[x + 1, y].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = temp;
                    candy1.setIndicies(tempX1, tempY1); candy2.setIndicies(tempX2, tempY2);

                    if (hasMatch) return true;
                }
            }
        }

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight - 1; y++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x, y + 1].isUsable && candyBoard[x, y].candy != null && candyBoard[x, y + 1].candy != null)
                {
                    // Tạm hoán đổi để kiểm tra
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x, y + 1].candy;
                    candyBoard[x, y + 1].candy = temp;

                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x, y + 1].candy.GetComponent<Candy>();
                    int tempX1 = candy1.xIndex, tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex, tempY2 = candy2.yIndex;
                    candy1.setIndicies(x, y); candy2.setIndicies(x, y + 1);

                    // Kiểm tra match
                    bool hasMatch = (IsConnected(candy1).connectionCandys.Count >= 3) || (IsConnected(candy2).connectionCandys.Count >= 3);

                    // Hoán đổi lại
                    candyBoard[x, y + 1].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = temp;
                    candy1.setIndicies(tempX1, tempY1); candy2.setIndicies(tempX2, tempY2);

                    if (hasMatch) return true;
                }
            }
        }

        return false;
    }

    public bool CheckBoard()
    {
        if (GameManager.instance.isGameOver) return false;

        bool hasMatch = false;
        candyToRemove.Clear();

        foreach (Node nodeCandy in candyBoard)
        {
            if (nodeCandy.candy != null) nodeCandy.candy.GetComponent<Candy>().isMatched = false;
        }

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
                {
                    Candy candy = candyBoard[x, y].candy.GetComponent<Candy>();
                    if (candy != null && !candy.isMatched)
                    {
                        MatchResult matchCandy = IsConnected(candy);
                        if (matchCandy.connectionCandys.Count >= 3)
                        {
                            MatchResult superMatchCandys = SuperMatch(matchCandy);
                            foreach (Candy c in superMatchCandys.connectionCandys)
                            {
                                if (!candyToRemove.Contains(c))
                                {
                                    candyToRemove.Add(c);
                                    c.isMatched = true;
                                }
                            }
                            hasMatch = true;
                        }
                    }
                }
            }
        }
        return hasMatch;
    }

    private HashSet<Candy> RemoveAndRefill(List<Candy> initialMatches)
    {
        HashSet<Candy> allCandiesToDestroySet = new HashSet<Candy>();
        Queue<Candy> processQueue = new Queue<Candy>();
        List<Candy> specialCandiesActivatedThisCycle = new List<Candy>();

        foreach (Candy candy in initialMatches)
        {
            if (candy != null && candy.gameObject.activeSelf && allCandiesToDestroySet.Add(candy))
            {
                processQueue.Enqueue(candy);
                candy.isMatched = true;
            }
        }

        while (processQueue.Count > 0)
        {
            Candy currentCandy = processQueue.Dequeue();
            if (currentCandy == null || !currentCandy.gameObject.activeSelf) continue;

            if (currentCandy.isSpecial && !specialCandiesActivatedThisCycle.Contains(currentCandy))
            {
                specialCandiesActivatedThisCycle.Add(currentCandy);
                List<Candy> newlyAffectedBySpecial = currentCandy.ExecuteSpecialEffectLogic(this, allCandiesToDestroySet);

                foreach (Candy newlyHitCandy in newlyAffectedBySpecial)
                {
                    if (newlyHitCandy != null && newlyHitCandy.gameObject.activeSelf && allCandiesToDestroySet.Add(newlyHitCandy))
                    {
                        newlyHitCandy.isMatched = true;
                        if (newlyHitCandy.isSpecial && !processQueue.Contains(newlyHitCandy) && !specialCandiesActivatedThisCycle.Contains(newlyHitCandy))
                        {
                            processQueue.Enqueue(newlyHitCandy);
                        }
                    }
                }
            }
        }

        CreateSpecialCandyIfMatch(initialMatches, allCandiesToDestroySet);

        HashSet<int> columnsToRefill = new HashSet<int>();
        foreach (Candy candyToReturn in allCandiesToDestroySet)
        {
            if (candyToReturn == null || !candyToReturn.gameObject.activeSelf) continue;

            int xIndex = candyToReturn.xIndex;
            int yIndex = candyToReturn.yIndex;
            columnsToRefill.Add(xIndex);

            if (candyBoard[xIndex, yIndex].candy == candyToReturn.gameObject)
            {
                candyBoard[xIndex, yIndex].candy = null;
            }
            _candyFactory.ReturnCandyToPool(candyToReturn);
        }

        foreach (int x in columnsToRefill.Distinct())
        {
            CollapseColumn(x);
            FillEmptySpacesInColumn(x);
        }
        return allCandiesToDestroySet;
    }

    private void CreateSpecialCandyIfMatch(List<Candy> matchedCandiesFromOriginalMatch, HashSet<Candy> allCandiesCurrentlyBeingDestroyed)
    {
        if (matchedCandiesFromOriginalMatch == null || matchedCandiesFromOriginalMatch.Count < 4) return;

        Candy primaryCandy = null;
        Candy currentSelected = GetSelectedCandy();
        if (currentSelected != null && matchedCandiesFromOriginalMatch.Contains(currentSelected))
        {
            primaryCandy = currentSelected;
        }
        else
        {
            List<Candy> sortedMatch = matchedCandiesFromOriginalMatch.OrderBy(c => c.xIndex).ThenBy(c => c.yIndex).ToList();
            if (sortedMatch.Any()) primaryCandy = sortedMatch[sortedMatch.Count / 2];
        }

        if (primaryCandy == null || !primaryCandy.gameObject.activeSelf) return;

        int specialX = primaryCandy.xIndex;
        int specialY = primaryCandy.yIndex;
        CandyType originalType = primaryCandy.candyType;
        Vector3 specialPosition = new Vector3((specialX - spaceingX) * spacingScale, (specialY - spaceingY) * spacingScale, primaryCandy.transform.position.z);

        bool isHorizontalMatch = matchedCandiesFromOriginalMatch.All(c => c.yIndex == matchedCandiesFromOriginalMatch[0].yIndex);
        bool isVerticalMatch = matchedCandiesFromOriginalMatch.All(c => c.xIndex == matchedCandiesFromOriginalMatch[0].xIndex);

        SpecialCandyEffect effectToCreate = SpecialCandyEffect.None;
        if (matchedCandiesFromOriginalMatch.Count >= 4)
        {
            if (isHorizontalMatch && !isVerticalMatch) effectToCreate = SpecialCandyEffect.ClearRow;
            else if (isVerticalMatch && !isHorizontalMatch) effectToCreate = SpecialCandyEffect.ClearColumn;
            else if (isHorizontalMatch && isVerticalMatch) // L or T shape
            {
                // Simple logic: prefer vertical if the vertical part of the T/L is longer or equal
                if (matchedCandiesFromOriginalMatch.Count(c => c.xIndex == specialX) >= matchedCandiesFromOriginalMatch.Count(c => c.yIndex == specialY))
                {
                    effectToCreate = SpecialCandyEffect.ClearColumn;
                }
                else
                {
                    effectToCreate = SpecialCandyEffect.ClearRow;
                }
            }
        }

        if (effectToCreate != SpecialCandyEffect.None)
        {
            Candy newSpecialCandy = _candyFactory.CreateSpecialCandy(originalType, effectToCreate, specialX, specialY, specialPosition);
            if (newSpecialCandy != null)
            {
                if (candyBoard[specialX, specialY].candy != null && candyBoard[specialX, specialY].candy != newSpecialCandy.gameObject)
                {
                    Debug.LogWarning($"Overwriting existing candy at [{specialX},{specialY}] to create special candy.");
                    _candyFactory.ReturnCandyToPool(candyBoard[specialX, specialY].candy.GetComponent<Candy>());
                }
                candyBoard[specialX, specialY].candy = newSpecialCandy.gameObject;
            }
        }
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
                        Vector3 targetPos = new Vector3((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale, candyToMove.transform.position.z);

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
                    for (int i = 0; i < this.candyPrefabs.Length; i++) availableTypes.Add(i);
                }
                int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];
                CandyType typeToCreate = (CandyType)randomIndex;

                Vector3 spawnPos = new Vector3((x - spaceingX) * spacingScale, (boardHeight - spaceingY) * spacingScale, 0);
                Vector3 targetPos = new Vector3((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale, 0);

                Candy newCandy = _candyFactory.CreateRegularCandy(typeToCreate, x, y, spawnPos);
                if (newCandy != null)
                {
                    newCandy.MoveToTarget(targetPos);
                    candyBoard[x, y] = new Node(true, newCandy.gameObject);
                }
                else
                {
                    Debug.LogError($"Failed to create candy via factory for refill at [{x},{y}].");
                }
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
                    extraConnectionCandys.AddRange(matchCandy.connectionCandys);
                    return new MatchResult() { connectionCandys = extraConnectionCandys, direction = MatchDirection.Super };
                }
            }
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
                    extraConnectionCandys.AddRange(matchCandy.connectionCandys);
                    return new MatchResult() { connectionCandys = extraConnectionCandys, direction = MatchDirection.Super };
                }
            }
        }
        return matchCandy; // Return original match if not super
    }

    MatchResult IsConnected(Candy candy)
    {
        List<Candy> horizontalCandys = new List<Candy> { candy };
        CheckDirection(candy, Vector2Int.right, horizontalCandys);
        CheckDirection(candy, Vector2Int.left, horizontalCandys);
        if (horizontalCandys.Count >= 4) return new MatchResult() { connectionCandys = horizontalCandys, direction = MatchDirection.LongHorizontal };
        if (horizontalCandys.Count == 3) return new MatchResult() { connectionCandys = horizontalCandys, direction = MatchDirection.Horizontal };

        List<Candy> verticalCandys = new List<Candy> { candy };
        CheckDirection(candy, Vector2Int.up, verticalCandys);
        CheckDirection(candy, Vector2Int.down, verticalCandys);
        if (verticalCandys.Count >= 4) return new MatchResult() { connectionCandys = verticalCandys, direction = MatchDirection.LongVertical };
        if (verticalCandys.Count == 3) return new MatchResult() { connectionCandys = verticalCandys, direction = MatchDirection.Vertical };

        return new MatchResult() { connectionCandys = new List<Candy>(), direction = MatchDirection.None };
    }

    void CheckDirection(Candy candy, Vector2Int direction, List<Candy> connectionCandys)
    {
        int x = candy.xIndex + direction.x;
        int y = candy.yIndex + direction.y;
        while (x >= 0 && x < boardWidth && y >= 0 && y < boardHeight)
        {
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
            {
                Candy nextCandy = candyBoard[x, y].candy.GetComponent<Candy>();
                if (nextCandy != null && !nextCandy.isMatched && nextCandy.candyType == candy.candyType)
                {
                    connectionCandys.Add(nextCandy);
                    x += direction.x;
                    y += direction.y;
                }
                else break;
            }
            else break;
        }
    }

    public void DoSwap(Candy firstCandy, Candy secondCandy)
    {
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("Cannot swap with null candy");
            return;
        }

        int firstX = firstCandy.xIndex, firstY = firstCandy.yIndex;
        int secondX = secondCandy.xIndex, secondY = secondCandy.yIndex;

        Vector3 firstPos = firstCandy.transform.position;
        Vector3 secondPos = secondCandy.transform.position;

        candyBoard[firstX, firstY].candy = secondCandy.gameObject;
        candyBoard[secondX, secondY].candy = firstCandy.gameObject;

        firstCandy.setIndicies(secondX, secondY);
        secondCandy.setIndicies(firstX, firstY);

        firstCandy.MoveToTarget(secondPos);
        secondCandy.MoveToTarget(firstPos);
    }

    public void ReportCandyClicked(Candy candy)
    {
        if (currentState != null && candy != null && !candy.isMoving)
        {
            currentState.HandleCandyClick(candy);
        }
        else if (candy != null && candy.isMoving)
        {
            Debug.Log($"Candy {candy.name} is moving. Click ignored.");
        }
    }

    public bool IsAdjacent(Candy firstCandy, Candy secondCandy)
    {
        if (firstCandy == null || secondCandy == null) return false;
        return Mathf.Abs(firstCandy.xIndex - secondCandy.xIndex) + Mathf.Abs(firstCandy.yIndex - secondCandy.yIndex) == 1;
    }
}

// Lớp MatchResult và Enum giữ nguyên, không cần thay đổi
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