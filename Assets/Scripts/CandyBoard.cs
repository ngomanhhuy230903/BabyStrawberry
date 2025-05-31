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
    // --- Biến cho Hệ thống Gợi ý ---
    [Header("Hint System Settings")] // THÊM MỚI SECTION NÀY
    public float hintDelay = 5f;
    public float hintAnimationDuration = 0.6f;
    public float hintAnimationScaleFactor = 1.15f;
    public int maxConcurrentHints = 1;

    public float _idleTimer = 0f; // THÊM MỚI (public để IdleState truy cập)
    public Coroutine _hintAnimationCoroutine; // THÊM MỚI (public để IdleState truy cập)
    public List<Candy[]> _listOfPossibleMoves = new List<Candy[]>(); // THÊM MỚI (public để IdleState truy cập)
    private List<Coroutine> _activeHintAnimations = new List<Coroutine>(); // THÊM MỚI

    // Tham chiếu đến GameManager
    public GameManager gameManager; // THÊM MỚI (gán trong Inspector)

    public void Awake()
    {
        instance = this;
        // THÊM MỚI: Gán GameManager nếu chưa có
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) Debug.LogError("CandyBoard Critical Error: GameManager not found!");

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
        if (candyPrefabs.Length > 0) // THÊM: Kiểm tra an toàn
        {
            for (int i = 0; i < candyPrefabs.Length; i++)
            {
                // THÊM: Kiểm tra an toàn hơn cho mảng row/column clearer
                if (rowClearerPrefabs.Length <= i || rowClearerPrefabs[i] == null) Debug.LogError($"rowClearerPrefabs[{i}] is null or out of bounds for candy type {(CandyType)i}.");
                if (columnClearerPrefabs.Length <= i || columnClearerPrefabs[i] == null) Debug.LogError($"columnClearerPrefabs[{i}] is null or out of bounds for candy type {(CandyType)i}.");
            }
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
        if (Input.GetKeyDown(KeyCode.M)) // THAY ĐỔI: CheckForPossibleMatches giờ cập nhật _listOfPossibleMoves
        {
            bool hasMoves = CheckForPossibleMatches();
            Debug.Log($"Board has possible matches: {hasMoves}. Found {_listOfPossibleMoves.Count} moves.");
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("Force showing hint via H key.");
            ResetIdleTimerAndStopHints();
            _listOfPossibleMoves = FindAllPossibleMoves();
            if (_listOfPossibleMoves.Count > 0 && _hintAnimationCoroutine == null)
            {
                _hintAnimationCoroutine = StartCoroutine(AnimateHintCandiesCoroutine());
            }
            _idleTimer = 0f;
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
        ResetIdleTimerAndStopHints(); // THÊM MỚI
        Debug.Log("Initializing board (Internal Coroutine)...");
        DeselectCurrentCandy();
        ClearEntireBoard();

        candyBoard = new Node[boardWidth, boardHeight];
        // THAY ĐỔI/GỢI Ý: Cách tính toán vị trí và spacing
        // spaceingX = (float)(boardWidth -1) / 2f * spacingScale;
        // spaceingY = (float)(boardHeight -1) / 2f * spacingScale;
        // Hoặc dựa trên camera:
        // float camHeight = Camera.main.orthographicSize * 2;
        // float camWidth = camHeight * Camera.main.aspect;
        // spacingScale = Mathf.Min(camWidth / (boardWidth + 1), camHeight / (boardHeight + 1));
        // Vector2 boardOrigin = new Vector2(-camWidth / 2 + spacingScale, -camHeight / 2 + spacingScale);


        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight) { Debug.LogError("arrayLayout is null or has incorrect row count."); yield break; }
        for (int y = 0; y < boardHeight; y++) { if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth) { Debug.LogError($"arrayLayout.rows[{y}].row is null or has incorrect length."); yield break; } }

        CreateBoardWithoutMatches();
        yield return new WaitForSeconds(0.1f);

        bool initialMatchesFound = CheckBoard();
        if (initialMatchesFound)
        {
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(false));
        }
        FinalizeBoardInitialization(); // Sẽ gọi CheckForPossibleMatches và ResetIdleTimerAndStopHints
    }

    private void FinalizeBoardInitialization()
    {
        ResetIdleTimerAndStopHints(); // THÊM MỚI
        if (!CheckForPossibleMatches()) // CheckForPossibleMatches giờ sẽ cập nhật _listOfPossibleMoves
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
    // Lưu ý: ProcessSwapAndMatchesCoroutine và ProcessTurnOnMatchedBoard đã được sửa ở lần trước, giữ nguyên
    public IEnumerator ProcessSwapAndMatchesCoroutine(Candy firstCandy, Candy secondCandy)
    {
        ResetIdleTimerAndStopHints(); // THÊM MỚI
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

        if (allDestroyedThisTurn.Count > 0 && gameManager != null) // THAY ĐỔI: Sử dụng biến gameManager
        {
            gameManager.ProcessTurn(allDestroyedThisTurn.Count, subtractMoves);
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

    public void FinalizeCurrentTurnProcessing()
    {
        Debug.Log("Finalizing current turn processing.");
        DeselectCurrentCandy();
        ResetIdleTimerAndStopHints(); // THÊM MỚI: Quan trọng

        if (!CheckForPossibleMatches() && !(currentState is InitializingBoardState)) // CheckForPossibleMatches giờ cập nhật _listOfPossibleMoves
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
                // THAY ĐỔI/GỢI Ý: Cách tính toán vị trí
                Vector3 position = new Vector3((x - (boardWidth - 1) / 2f) * spacingScale, (y - (boardHeight - 1) / 2f) * spacingScale, 0);
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
    public List<Candy[]> FindAllPossibleMoves()
    {
        if (gameManager != null && gameManager.isGameOver) return new List<Candy[]>();

        List<Candy[]> possibleMoves = new List<Candy[]>();
        if (candyBoard == null)
        {
            Debug.LogError("candyBoard is null in FindAllPossibleMoves.");
            return possibleMoves;
        }

        // Kiểm tra swap ngang
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth - 1; x++)
            {
                if (candyBoard[x, y]?.isUsable == true && candyBoard[x + 1, y]?.isUsable == true &&
                    candyBoard[x, y]?.candy != null && candyBoard[x + 1, y]?.candy != null)
                {
                    Candy candy1Original = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2Original = candyBoard[x + 1, y].candy.GetComponent<Candy>();

                    GameObject tempGO = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x + 1, y].candy;
                    candyBoard[x + 1, y].candy = tempGO;

                    int originalX1 = candy1Original.xIndex, originalY1 = candy1Original.yIndex;
                    int originalX2 = candy2Original.xIndex, originalY2 = candy2Original.yIndex;

                    candy1Original.setIndicies(originalX2, originalY2);
                    candy2Original.setIndicies(originalX1, originalY1);

                    if (IsConnected(candy1Original).connectionCandys.Count >= 3 || IsConnected(candy2Original).connectionCandys.Count >= 3)
                    {
                        possibleMoves.Add(new Candy[] { candyBoard[x + 1, y].candy.GetComponent<Candy>(), candyBoard[x, y].candy.GetComponent<Candy>() });
                    }

                    candyBoard[x, y].candy = candy1Original.gameObject;
                    candyBoard[x + 1, y].candy = candy2Original.gameObject;
                    candy1Original.setIndicies(originalX1, originalY1);
                    candy2Original.setIndicies(originalX2, originalY2);
                }
            }
        }

        // Kiểm tra swap dọc (tương tự như trên)
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight - 1; y++)
            {
                if (candyBoard[x, y]?.isUsable == true && candyBoard[x, y + 1]?.isUsable == true &&
                   candyBoard[x, y]?.candy != null && candyBoard[x, y + 1]?.candy != null)
                {
                    Candy candy1Original = candyBoard[x, y].candy.GetComponent<Candy>();
                    Candy candy2Original = candyBoard[x, y + 1].candy.GetComponent<Candy>();

                    GameObject tempGO = candyBoard[x, y].candy;
                    candyBoard[x, y].candy = candyBoard[x, y + 1].candy;
                    candyBoard[x, y + 1].candy = tempGO;

                    int originalX1 = candy1Original.xIndex, originalY1 = candy1Original.yIndex;
                    int originalX2 = candy2Original.xIndex, originalY2 = candy2Original.yIndex;

                    candy1Original.setIndicies(originalX2, originalY2);
                    candy2Original.setIndicies(originalX1, originalY1);

                    if (IsConnected(candy1Original).connectionCandys.Count >= 3 || IsConnected(candy2Original).connectionCandys.Count >= 3)
                    {
                        possibleMoves.Add(new Candy[] { candyBoard[x, y + 1].candy.GetComponent<Candy>(), candyBoard[x, y].candy.GetComponent<Candy>() });
                    }

                    candyBoard[x, y].candy = candy1Original.gameObject;
                    candyBoard[x, y + 1].candy = candy2Original.gameObject;
                    candy1Original.setIndicies(originalX1, originalY1);
                    candy2Original.setIndicies(originalX2, originalY2);
                }
            }
        }
        return possibleMoves;
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
        if (gameManager != null && gameManager.isGameOver) return false;
        _listOfPossibleMoves = FindAllPossibleMoves();
        return _listOfPossibleMoves.Count > 0;
    }
    // THÊM MỚI
    public IEnumerator AnimateHintCandiesCoroutine()
    {
        if (_listOfPossibleMoves.Count == 0)
        {
            _hintAnimationCoroutine = null;
            yield break;
        }
        System.Random rng = new System.Random();
        _listOfPossibleMoves = _listOfPossibleMoves.OrderBy(a => rng.Next()).ToList();
        StopAndClearActiveHintAnimations();
        int hintsToActuallyShow = Mathf.Min(maxConcurrentHints, _listOfPossibleMoves.Count);
        for (int i = 0; i < hintsToActuallyShow; i++)
        {
            Candy[] move = _listOfPossibleMoves[i];
            if (move.Length == 2 && move[0] != null && move[1] != null &&
                move[0].gameObject.activeInHierarchy && move[1].gameObject.activeInHierarchy &&
                !move[0].isMoving && !move[1].isMoving)
            {
                Coroutine anim1 = StartCoroutine(SingleCandyHintAnimation(move[0]));
                Coroutine anim2 = StartCoroutine(SingleCandyHintAnimation(move[1]));
                if (anim1 != null) _activeHintAnimations.Add(anim1);
                if (anim2 != null) _activeHintAnimations.Add(anim2);
            }
        }
        yield return new WaitForSeconds(hintAnimationDuration + 0.2f);
        _idleTimer = 0f;
        _hintAnimationCoroutine = null;
    }

    // THÊM MỚI
    private IEnumerator SingleCandyHintAnimation(Candy candy)
    {
        if (candy == null || !candy.gameObject.activeInHierarchy || candy.isMoving) yield break;
        Vector3 originalScale = candy.originalScale;
        Vector3 targetScale = originalScale * hintAnimationScaleFactor;
        float halfDuration = hintAnimationDuration / 2;
        float timer = 0f;
        while (timer < halfDuration)
        {
            if (candy == null || !candy.gameObject.activeInHierarchy || candy.isMoving)
            {
                if (candy != null) candy.transform.localScale = originalScale;
                yield break;
            }
            candy.transform.localScale = Vector3.Lerp(originalScale, targetScale, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        if (candy == null || !candy.gameObject.activeInHierarchy || candy.isMoving)
        {
            if (candy != null) candy.transform.localScale = originalScale;
            yield break;
        }
        candy.transform.localScale = targetScale;
        timer = 0f;
        while (timer < halfDuration)
        {
            if (candy == null || !candy.gameObject.activeInHierarchy || candy.isMoving)
            {
                if (candy != null) candy.transform.localScale = originalScale;
                yield break;
            }
            candy.transform.localScale = Vector3.Lerp(targetScale, originalScale, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        if (candy != null && candy.gameObject.activeInHierarchy && !candy.isMoving)
        {
            candy.transform.localScale = originalScale;
        }
    }

    // THÊM MỚI
    public void ResetIdleTimerAndStopHints()
    {
        _idleTimer = 0f;
        if (_hintAnimationCoroutine != null)
        {
            StopCoroutine(_hintAnimationCoroutine);
            _hintAnimationCoroutine = null;
        }
        StopAndClearActiveHintAnimations();
    }

    // THÊM MỚI
    private void StopAndClearActiveHintAnimations()
    {
        foreach (var animCoroutine in _activeHintAnimations)
        {
            if (animCoroutine != null) StopCoroutine(animCoroutine);
        }
        _activeHintAnimations.Clear();
        if (candyBoard != null)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    if (candyBoard[x, y]?.candy != null && candyBoard[x, y].isUsable)
                    {
                        Candy c = candyBoard[x, y].candy.GetComponent<Candy>();
                        if (c != null && c.gameObject.activeInHierarchy && c.transform.localScale != c.originalScale)
                        {
                            c.transform.localScale = c.originalScale;
                        }
                    }
                }
            }
        }
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