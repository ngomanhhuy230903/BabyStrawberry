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
    public GameObject colorBombPrefab;
    public Node[,] candyBoard;
    public GameObject candyParent;
    [SerializeField] List<Candy> candyToRemove = new List<Candy>();

    [SerializeField] private Candy _selectedCandy;

    private IBoardState currentState;
    public ArrayLayout arrayLayout;
    public static CandyBoard instance;
    private CandyFactory _candyFactory;

    [Header("Pooling Settings")]
    public int initialPoolSizePerType = 30;
    [Header("Hint Settings")]
    public float hintDelay = 5f; // Thời gian (giây) chờ trước khi hiển thị gợi ý
    public float hintDisplayDuration = 1.5f; // Thời gian (giây) hiển thị hoạt ảnh gợi ý
    private List<Candy> _hintCandies = new List<Candy>();
    private Coroutine _hintAnimationCoroutine;
    private Coroutine _hintDelayCoroutine; // Coroutine để quản lý độ trễ trước khi hiển thị gợi ý

    public void Awake()
    {
        instance = this;
        ValidateSpecialPrefabs();
        if (candyParent == null) Debug.LogError("CandyBoard Critical Error: candyParent is not assigned in Inspector!");
        if (candyPrefabs == null || candyPrefabs.Length == 0) Debug.LogError("CandyBoard Critical Error: candyPrefabs array is not assigned or empty in Inspector!");

        _candyFactory = new CandyFactory(this.candyPrefabs, this.rowClearerPrefabs, this.columnClearerPrefabs, this.colorBombPrefab, this.candyParent.transform, initialPoolSizePerType);
    }
    public CandyFactory GetCandyFactory() { return _candyFactory; }
    public void Start()
    {
        if (_candyFactory == null)
        {
            Debug.LogError("CandyFactory was not initialized in Awake. Aborting Start.");
            return;
        }
        SetState(new InitializingBoardState(this));
    }

    public new void SetState(IBoardState newState) // Thêm 'new' nếu có cảnh báo về việc ẩn thành viên kế thừa
    {
        currentState?.OnExit();

        // Dừng và reset timer gợi ý mỗi khi chuyển state
        ResetIdleTimer(); // Điều này cũng gọi StopHint()

        currentState = newState;
        if (currentState != null)
        {
            currentState.OnEnter();
            if (currentState is IdleState) // Nếu state mới là Idle, bắt đầu timer
            {
                StartIdleTimer();
            }
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

    public new void Update() // Thêm 'new' nếu có cảnh báo
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Reset timer gợi ý khi có tương tác chuột
            // Việc này sẽ được xử lý tốt hơn bởi SetState nếu click dẫn đến thay đổi state.
            // Tuy nhiên, để chắc chắn, nếu đang ở Idle và có click, ta reset ngay.
            if (currentState is IdleState)
            {
                // ResetIdleTimer(); // Dòng này có thể không cần thiết nếu HandleCandyClick luôn dẫn đến SetState
            }

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Candy"));
            if (hit.collider != null)
            {
                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
                if (candy != null && currentState != null)
                {
                    // HandleCandyClick trong IdleState sẽ gọi ResetIdleTimer và StopHint
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
            List<Candy> hint = FindHintMove();
            if (hint != null) Debug.Log($"Hint found: {hint[0].name} and {hint[1].name}"); else Debug.Log("No hint move found.");
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

    private new void FinalizeBoardInitialization() // Thêm 'new' nếu có cảnh báo
    {
        // ResetIdleTimer() sẽ được gọi trong SetState()
        if (!CheckForPossibleMatches())
        {
            Debug.Log("No possible matches on the board after init/initial processing, re-triggering initialization...");
            SetState(new InitializingBoardState(this));
        }
        else
        {
            Debug.Log("Board initialization complete with valid moves available.");
            SetState(new IdleState(this)); // SetState sẽ tự động gọi StartIdleTimer
        }
    }

    // Lưu ý: ProcessSwapAndMatchesCoroutine và ProcessTurnOnMatchedBoard đã được sửa ở lần trước, giữ nguyên
    public IEnumerator ProcessSwapAndMatchesCoroutine(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log($"=== ProcessSwapAndMatchesCoroutine START: {firstCandy.name} with {secondCandy.name} ===");
        bool isColorBombSwap = firstCandy.specialEffect == SpecialCandyEffect.ClearColor || secondCandy.specialEffect == SpecialCandyEffect.ClearColor;

        if (isColorBombSwap)
        {
            Candy colorBomb = firstCandy.specialEffect == SpecialCandyEffect.ClearColor ? firstCandy : secondCandy;
            Candy otherCandy = colorBomb == firstCandy ? secondCandy : firstCandy;
            yield return new WaitForSeconds(0.1f);
            List<Candy> initialDestructionList = new List<Candy> { colorBomb, otherCandy };
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(true, initialDestructionList, colorBomb, otherCandy));
        }
        else
        {
            DoSwap(firstCandy, secondCandy);
            yield return new WaitForSeconds(0.3f);

            bool hasMatch = CheckBoard();
            if (hasMatch)
            {
                yield return StartCoroutine(ProcessTurnOnMatchedBoard(true, new List<Candy>(this.candyToRemove)));
            }
            else
            {
                DoSwap(firstCandy, secondCandy);
                yield return new WaitForSeconds(0.3f);
                FinalizeCurrentTurnProcessing();
            }
        }
        Debug.Log("=== ProcessSwapAndMatchesCoroutine END ===");
    }

    public IEnumerator ProcessTurnOnMatchedBoard(bool subtractMoves, List<Candy> initialMatches = null, Candy activator = null, Candy target = null)
    {
        List<Candy> candiesToProcess = initialMatches ?? new List<Candy>(this.candyToRemove);

        if (candiesToProcess.Count == 0)
        {
            FinalizeCurrentTurnProcessing();
            yield break;
        }

        HashSet<Candy> allDestroyedThisTurn = RemoveAndRefill(candiesToProcess, activator, target);

        if (allDestroyedThisTurn.Count > 0 && GameManager.instance != null)
        {
            GameManager.instance.ProcessTurn(allDestroyedThisTurn.Count, subtractMoves);
        }

        this.candyToRemove.Clear();
        yield return new WaitForSeconds(0.4f);

        if (CheckBoard())
        {
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(false, new List<Candy>(this.candyToRemove))); // Cascade
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
            FinalizeCurrentTurnProcessing();
        }
    }

    public new void FinalizeCurrentTurnProcessing() // Thêm 'new' nếu có cảnh báo
    {
        Debug.Log("Finalizing current turn processing.");
        DeselectCurrentCandy();

        // ResetIdleTimer() sẽ được gọi trong SetState()
        // StopHint() cũng sẽ được gọi trong ResetIdleTimer()

        if (!CheckForPossibleMatches() && !(currentState is InitializingBoardState))
        {
            Debug.Log("No more possible matches after turn, setting NoPossibleMovesState.");
            SetState(new NoPossibleMovesState(this));
        }
        else if (!(currentState is InitializingBoardState))
        {
            Debug.Log("Turn processing complete, moves available, setting IdleState.");
            SetState(new IdleState(this)); // SetState sẽ tự động gọi StartIdleTimer
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
        if (_candyFactory != null && candyBoard != null)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    if (candyBoard[x, y]?.isUsable == true && candyBoard[x, y]?.candy != null)
                    {
                        Candy candyComponent = candyBoard[x, y].candy.GetComponent<Candy>();
                        if (candyComponent != null) _candyFactory.ReturnCandyToPool(candyComponent);
                        else Destroy(candyBoard[x, y].candy);
                        candyBoard[x, y].candy = null;
                    }
                }
            }
        }
        candyToRemove?.Clear();
        DeselectCurrentCandy();
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
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x + 1, y].candy;
                    candyBoard[x + 1, y].candy = temp;

                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x + 1, y].candy.GetComponent<Candy>();
                    int tempX1 = candy1.xIndex, tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex, tempY2 = candy2.yIndex;
                    candy1.setIndicies(x, y); candy2.setIndicies(x + 1, y);

                    bool hasMatch = (IsConnected(candy1).connectionCandys.Count >= 3) || (IsConnected(candy2).connectionCandys.Count >= 3);

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
                    GameObject temp = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x, y + 1].candy;
                    candyBoard[x, y + 1].candy = temp;

                    Candy candy1 = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2 = candyBoard[x, y + 1].candy.GetComponent<Candy>();
                    int tempX1 = candy1.xIndex, tempY1 = candy1.yIndex;
                    int tempX2 = candy2.xIndex, tempY2 = candy2.yIndex;
                    candy1.setIndicies(x, y); candy2.setIndicies(x, y + 1);

                    bool hasMatch = (IsConnected(candy1).connectionCandys.Count >= 3) || (IsConnected(candy2).connectionCandys.Count >= 3);

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

    // Hàm RemoveAndRefill đã được sửa ở lần trước, giữ nguyên
    private HashSet<Candy> RemoveAndRefill(List<Candy> initialMatches, Candy swapActivator = null, Candy swapTarget = null)
    {
        HashSet<Candy> allCandiesToDestroySet = new HashSet<Candy>();
        Queue<Candy> processQueue = new Queue<Candy>();

        if (swapActivator != null && swapActivator.specialEffect == SpecialCandyEffect.ClearColor)
        {
            SpecialCandyEffect effect;
            if (swapTarget.specialEffect == SpecialCandyEffect.ClearColor)
            {
                effect = SpecialCandyEffect.ClearBoard;
            }
            else if (swapTarget.isSpecial)
            {
                effect = SpecialCandyEffect.UpgradeColorToSpecials;
            }
            else
            {
                effect = SpecialCandyEffect.ClearColor;
            }

            swapActivator.SetStrategyBasedOnEffect(effect);
            List<Candy> affected = swapActivator.ExecuteSpecialEffectLogic(this, swapTarget, allCandiesToDestroySet);
            foreach (var candy in affected)
            {
                if (allCandiesToDestroySet.Add(candy)) processQueue.Enqueue(candy);
            }
            allCandiesToDestroySet.Add(swapActivator);
            allCandiesToDestroySet.Add(swapTarget);
        }
        else
        {
            foreach (Candy candy in initialMatches)
            {
                if (allCandiesToDestroySet.Add(candy)) processQueue.Enqueue(candy);
            }
        }

        while (processQueue.Count > 0)
        {
            Candy currentCandy = processQueue.Dequeue();
            if (currentCandy == null || !currentCandy.gameObject.activeSelf) continue;

            if (currentCandy.isSpecial)
            {
                if (currentCandy.specialEffect == SpecialCandyEffect.ClearRow || currentCandy.specialEffect == SpecialCandyEffect.ClearColumn)
                {
                    List<Candy> newlyAffected = currentCandy.ExecuteSpecialEffectLogic(this, null, allCandiesToDestroySet);
                    foreach (Candy newlyHitCandy in newlyAffected)
                    {
                        if (allCandiesToDestroySet.Add(newlyHitCandy))
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

        // --- Logic tìm kẹo trung tâm và tọa độ vẫn giữ nguyên ---
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

        bool isStraightHorizontal = matchedCandiesFromOriginalMatch.All(c => c.yIndex == primaryCandy.yIndex);
        // Kiểm tra xem tất cả kẹo trong match có nằm trên cùng một hàng dọc không
        bool isStraightVertical = matchedCandiesFromOriginalMatch.All(c => c.xIndex == primaryCandy.xIndex);

        if (matchedCandiesFromOriginalMatch.Count >= 5 && (isStraightHorizontal || isStraightVertical))
        {
            Debug.Log("Creating Color Bomb due to straight line match-5.");
            Candy newSpecialCandy = _candyFactory.CreateSpecialCandy(originalType, SpecialCandyEffect.ClearColor, specialX, specialY, specialPosition);
            if (newSpecialCandy != null)
            {
                if (candyBoard[specialX, specialY].candy != null)
                {
                    _candyFactory.ReturnCandyToPool(candyBoard[specialX, specialY].candy.GetComponent<Candy>());
                }
                candyBoard[specialX, specialY].candy = newSpecialCandy.gameObject;
                allCandiesCurrentlyBeingDestroyed.Remove(primaryCandy);
            }
            return; // Đã xử lý, thoát khỏi hàm
        }

        if (matchedCandiesFromOriginalMatch.Count == 4 && (isStraightHorizontal || isStraightVertical))
        {
            SpecialCandyEffect effectToCreate = isStraightHorizontal ? SpecialCandyEffect.ClearRow : SpecialCandyEffect.ClearColumn;
            Debug.Log($"Creating {effectToCreate} due to straight line match-4.");

            Candy newSpecialCandy = _candyFactory.CreateSpecialCandy(originalType, effectToCreate, specialX, specialY, specialPosition);
            if (newSpecialCandy != null)
            {
                if (candyBoard[specialX, specialY].candy != null)
                {
                    _candyFactory.ReturnCandyToPool(candyBoard[specialX, specialY].candy.GetComponent<Candy>());
                }
                candyBoard[specialX, specialY].candy = newSpecialCandy.gameObject;
                allCandiesCurrentlyBeingDestroyed.Remove(primaryCandy);
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
        if (matchCandy.direction == MatchDirection.Horizontal || matchCandy.direction == MatchDirection.LongHorizontal || matchCandy.direction == MatchDirection.Super)
        {
            foreach (Candy candy in matchCandy.connectionCandys.ToList()) // ToList() để tránh lỗi thay đổi collection khi duyệt
            {
                List<Candy> extraConnectionCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.up, extraConnectionCandys);
                CheckDirection(candy, Vector2Int.down, extraConnectionCandys);
                if (extraConnectionCandys.Count >= 2)
                {
                    foreach (var extraCandy in extraConnectionCandys)
                    {
                        if (!matchCandy.connectionCandys.Contains(extraCandy))
                        {
                            matchCandy.connectionCandys.Add(extraCandy);
                        }
                    }
                    matchCandy.direction = MatchDirection.Super;
                }
            }
        }

        if (matchCandy.direction == MatchDirection.Vertical || matchCandy.direction == MatchDirection.LongVertical || matchCandy.direction == MatchDirection.Super)
        {
            foreach (Candy candy in matchCandy.connectionCandys.ToList())
            {
                List<Candy> extraConnectionCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.right, extraConnectionCandys);
                CheckDirection(candy, Vector2Int.left, extraConnectionCandys);
                if (extraConnectionCandys.Count >= 2)
                {
                    foreach (var extraCandy in extraConnectionCandys)
                    {
                        if (!matchCandy.connectionCandys.Contains(extraCandy))
                        {
                            matchCandy.connectionCandys.Add(extraCandy);
                        }
                    }
                    matchCandy.direction = MatchDirection.Super;
                }
            }
        }
        return matchCandy;
    }

    MatchResult IsConnected(Candy candy)
    {
        List<Candy> horizontalCandys = new List<Candy> { candy };
        CheckDirection(candy, Vector2Int.right, horizontalCandys);
        CheckDirection(candy, Vector2Int.left, horizontalCandys);

        List<Candy> verticalCandys = new List<Candy> { candy };
        CheckDirection(candy, Vector2Int.up, verticalCandys);
        CheckDirection(candy, Vector2Int.down, verticalCandys);

        bool isFiveOrMoreHor = horizontalCandys.Count >= 5;
        bool isFiveOrMoreVer = verticalCandys.Count >= 5;

        if (isFiveOrMoreHor || isFiveOrMoreVer)
        {
            return new MatchResult() { connectionCandys = isFiveOrMoreHor ? horizontalCandys : verticalCandys, direction = MatchDirection.Super };
        }

        if (horizontalCandys.Count >= 3) return new MatchResult() { connectionCandys = horizontalCandys, direction = horizontalCandys.Count == 4 ? MatchDirection.LongHorizontal : MatchDirection.Horizontal };
        if (verticalCandys.Count >= 3) return new MatchResult() { connectionCandys = verticalCandys, direction = verticalCandys.Count == 4 ? MatchDirection.LongVertical : MatchDirection.Vertical };

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
    public void StartIdleTimer()
    {
        ResetIdleTimer(); // Đảm bảo timer cũ đã được dừng
        if (currentState is IdleState) // Chỉ bắt đầu nếu đang ở IdleState
        {
            _hintDelayCoroutine = StartCoroutine(HintDelayCoroutine());
        }
    }

    // Phương thức để reset (đặt lại) bộ đếm thời gian gợi ý
    public void ResetIdleTimer()
    {
        if (_hintDelayCoroutine != null)
        {
            StopCoroutine(_hintDelayCoroutine);
            _hintDelayCoroutine = null;
        }
        StopHint(); // Cũng dừng luôn hoạt ảnh gợi ý nếu có
    }

    // Coroutine đếm ngược thời gian trước khi hiển thị gợi ý
    private IEnumerator HintDelayCoroutine()
    {
        yield return new WaitForSeconds(hintDelay);
        // Kiểm tra lại xem có còn ở IdleState không và chưa có gợi ý nào đang chạy
        if (currentState is IdleState && _hintAnimationCoroutine == null)
        {
            List<Candy> possibleMove = FindHintMove();
            if (possibleMove != null && possibleMove.Count == 2)
            {
                ShowHint(possibleMove);
            }
            // Nếu không tìm thấy nước đi nào, có thể không làm gì hoặc bắt đầu lại timer
            // ở đây, chúng ta sẽ để nó tự kết thúc và IdleState sẽ bắt đầu lại nếu cần
        }
        _hintDelayCoroutine = null; // Coroutine đã hoàn thành
    }

    // Phương thức tìm một nước đi gợi ý (trả về 2 kẹo có thể tạo match)
    // Logic tương tự CheckForPossibleMatches nhưng trả về danh sách kẹo
    public List<Candy> FindHintMove()
    {
        if (GameManager.instance != null && GameManager.instance.isGameOver) return null;

        // Kiểm tra swap ngang
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth - 1; x++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x + 1, y].isUsable &&
                    candyBoard[x, y].candy != null && candyBoard[x + 1, y].candy != null)
                {
                    Candy c1Original = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy c2Original = candyBoard[x + 1, y].candy.GetComponent<Candy>();

                    // Lưu trữ GameObject để hoán đổi trong candyBoard
                    GameObject tempCandyGO = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x + 1, y].candy;
                    candyBoard[x + 1, y].candy = tempCandyGO;

                    // Lưu trữ và cập nhật chỉ số tạm thời cho việc kiểm tra
                    int originalC1X = c1Original.xIndex, originalC1Y = c1Original.yIndex;
                    int originalC2X = c2Original.xIndex, originalC2Y = c2Original.yIndex;
                    c1Original.setIndicies(originalC2X, originalC2Y); // c1 đến vị trí của c2
                    c2Original.setIndicies(originalC1X, originalC1Y); // c2 đến vị trí của c1


                    bool matchFound = (IsConnected(c1Original).connectionCandys.Count >= 3) ||
                                      (IsConnected(c2Original).connectionCandys.Count >= 3);

                    // Hoán đổi lại trạng thái ban đầu trong candyBoard và chỉ số của kẹo
                    candyBoard[x + 1, y].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = tempCandyGO;
                    c1Original.setIndicies(originalC1X, originalC1Y);
                    c2Original.setIndicies(originalC2X, originalC2Y);

                    if (matchFound)
                    {
                        // Trả về 2 kẹo gốc ở vị trí ban đầu của chúng
                        return new List<Candy> { this.candyBoard[x, y].candy.GetComponent<Candy>(), this.candyBoard[x + 1, y].candy.GetComponent<Candy>() };
                    }
                }
            }
        }

        // Kiểm tra swap dọc
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight - 1; y++)
            {
                if (candyBoard[x, y].isUsable && candyBoard[x, y + 1].isUsable &&
                    candyBoard[x, y].candy != null && candyBoard[x, y + 1].candy != null)
                {
                    Candy c1Original = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy c2Original = candyBoard[x, y + 1].candy.GetComponent<Candy>();

                    GameObject tempCandyGO = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x, y + 1].candy;
                    candyBoard[x, y + 1].candy = tempCandyGO;

                    int originalC1X = c1Original.xIndex, originalC1Y = c1Original.yIndex;
                    int originalC2X = c2Original.xIndex, originalC2Y = c2Original.yIndex;
                    c1Original.setIndicies(originalC2X, originalC2Y);
                    c2Original.setIndicies(originalC1X, originalC1Y);

                    bool matchFound = (IsConnected(c1Original).connectionCandys.Count >= 3) ||
                                      (IsConnected(c2Original).connectionCandys.Count >= 3);

                    candyBoard[x, y + 1].candy = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = tempCandyGO;
                    c1Original.setIndicies(originalC1X, originalC1Y);
                    c2Original.setIndicies(originalC2X, originalC2Y);

                    if (matchFound)
                    {
                        return new List<Candy> { this.candyBoard[x, y].candy.GetComponent<Candy>(), this.candyBoard[x, y + 1].candy.GetComponent<Candy>() };
                    }
                }
            }
        }
        return null; // Không tìm thấy nước đi nào
    }

    // Phương thức hiển thị gợi ý
    private void ShowHint(List<Candy> candiesToShow)
    {
        if (candiesToShow == null || candiesToShow.Count != 2 || _hintAnimationCoroutine != null) return;

        // Đảm bảo cả hai kẹo đều hợp lệ
        if (candiesToShow[0] == null || !candiesToShow[0].gameObject.activeSelf ||
            candiesToShow[1] == null || !candiesToShow[1].gameObject.activeSelf)
        {
            Debug.LogWarning("Hint: One or both candies for hint are invalid.");
            return;
        }

        _hintCandies = new List<Candy>(candiesToShow);
        _hintAnimationCoroutine = StartCoroutine(HintAnimationCoroutine(_hintCandies));
    }

    // Phương thức dừng hoạt ảnh gợi ý
    public void StopHint()
    {
        if (_hintAnimationCoroutine != null)
        {
            StopCoroutine(_hintAnimationCoroutine);
            _hintAnimationCoroutine = null;
            // Reset lại trạng thái hiển thị của kẹo gợi ý (ví dụ: scale)
            foreach (Candy c in _hintCandies)
            {
                if (c != null && c.gameObject.activeSelf) // Kiểm tra null và active
                {
                    c.transform.localScale = Vector3.one; // Giả sử scale mặc định là 1,1,1
                                                          // Nếu bạn có thay đổi màu sắc hoặc outline, reset ở đây
                }
            }
            _hintCandies.Clear();
        }
    }

    // Coroutine thực hiện hoạt ảnh cho kẹo gợi ý
    private IEnumerator HintAnimationCoroutine(List<Candy> candies)
    {
        if (candies == null || candies.Count != 2) yield break;
        Candy candy1 = candies[0];
        Candy candy2 = candies[1];

        // Kiểm tra lại trước khi bắt đầu animation
        if (candy1 == null || !candy1.gameObject.activeSelf || candy2 == null || !candy2.gameObject.activeSelf)
        {
            _hintCandies.Clear();
            _hintAnimationCoroutine = null;
            yield break;
        }

        float timer = 0f;
        Vector3 originalScale1 = candy1.transform.localScale;
        Vector3 originalScale2 = candy2.transform.localScale;
        float animationSpeed = 2.5f; // Tốc độ "rung lắc"
        float scaleAmount = 0.12f;   // Biên độ phóng to/thu nhỏ

        while (timer < hintDisplayDuration)
        {
            // Kiểm tra liên tục nếu game over, kẹo bị hủy hoặc di chuyển
            if ((GameManager.instance != null && GameManager.instance.isGameOver) ||
                candy1 == null || !candy1.gameObject.activeSelf || candy1.isMoving ||
                candy2 == null || !candy2.gameObject.activeSelf || candy2.isMoving)
            {
                // Nếu có vấn đề, dừng hint và thoát
                if (candy1 != null && candy1.gameObject.activeSelf) candy1.transform.localScale = originalScale1;
                if (candy2 != null && candy2.gameObject.activeSelf) candy2.transform.localScale = originalScale2;
                _hintCandies.Clear();
                _hintAnimationCoroutine = null;
                // Quan trọng: Sau khi hint bị ngắt giữa chừng, bắt đầu lại idle timer nếu vẫn ở IdleState
                if (currentState is IdleState) StartIdleTimer();
                yield break;
            }

            float scaleFactor = 1 + (Mathf.Sin(Time.time * Mathf.PI * animationSpeed) * scaleAmount);
            candy1.transform.localScale = originalScale1 * scaleFactor;
            candy2.transform.localScale = originalScale2 * scaleFactor;

            timer += Time.deltaTime;
            yield return null;
        }

        // Reset scale và dọn dẹp
        if (candy1 != null && candy1.gameObject.activeSelf) candy1.transform.localScale = originalScale1;
        if (candy2 != null && candy2.gameObject.activeSelf) candy2.transform.localScale = originalScale2;

        _hintCandies.Clear();
        _hintAnimationCoroutine = null;

        // Sau khi một gợi ý hoàn thành, bắt đầu lại bộ đếm thời gian cho gợi ý tiếp theo nếu người chơi vẫn không làm gì
        if (currentState is IdleState)
        {
            StartIdleTimer();
        }
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