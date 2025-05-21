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
    public List<GameObject> candyToDestroy = new();
    public GameObject candyParent;
    [SerializeField] private Candy selectedCandy;
    [SerializeField] public bool isProcessingMove;
    [SerializeField] List<Candy> candyToRemove = new List<Candy>();
    private bool isInitializingBoard = false;

    [SerializeField] private Candy _selectedCandy; // Giữ lại để các state có thể truy cập nếu cần, hoặc truyền qua constructor state

    private IBoardState currentState; // THAY ĐỔI: Trạng thái hiện tại của board
    public ArrayLayout arrayLayout;
    public static CandyBoard instance;
    private CandyFactory _candyFactory;
    [Header("Pooling Settings")]
    public int initialPoolSizePerType = 30; // Có thể điều chỉnh từ Inspector


    public void Awake()
    {
        instance = this;
        ValidateSpecialPrefabs(); // Giữ nguyên
        if (candyParent == null) Debug.LogError("CandyBoard Critical Error: candyParent is not assigned in Inspector!");
        if (candyPrefabs == null || candyPrefabs.Length == 0) Debug.LogError("CandyBoard Critical Error: candyPrefabs array is not assigned or empty in Inspector!");

        // Khởi tạo CandyFactory với initialPoolSize
        _candyFactory = new CandyFactory(this.candyPrefabs, this.rowClearerPrefabs, this.columnClearerPrefabs, this.candyParent.transform, initialPoolSizePerType);
    }
    public void Start()
    {
        if (_candyFactory == null)
        {
            Debug.LogError("CandyFactory was not initialized in Awake. Aborting Start.");
            return; // Không thể tiếp tục nếu factory null
        }
        SetState(new InitializingBoardState(this));
    }
    public void SetState(IBoardState newState)
    {
        if (currentState != null)
        {
            currentState.OnExit();
        }
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

    public void Update()
    {
        // Xử lý input chung ở đây, sau đó delegate cho state
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("Candy"));
            if (hit.collider != null)
            {
                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
                if (candy != null && currentState != null) // Thêm kiểm tra currentState != null
                {
                    currentState.HandleCandyClick(candy);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Space)) // Giữ lại để test nhanh việc reset board
        {
            Debug.Log("Spacebar pressed: Re-initializing board via new InitializingBoardState.");
            SetState(new InitializingBoardState(this)); // Khởi tạo lại bằng cách set state
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


        if (currentState != null) // Thêm kiểm tra currentState != null
        {
            currentState.UpdateState();
        }
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

        if (candyPrefabs == null || candyPrefabs.Length == 0) { Debug.LogError("candyPrefabs array is null or empty in InitializeBoard."); yield break; }
        for (int i = 0; i < candyPrefabs.Length; i++) { /* ... (kiểm tra null, SpriteRenderer, sprite cho từng prefab) ... */ }
        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight) { Debug.LogError("arrayLayout is null or has incorrect row count."); yield break; }
        for (int y = 0; y < boardHeight; y++) { if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth) { Debug.LogError($"arrayLayout.rows[{y}].row is null or has incorrect length."); yield break; } }

        CreateBoardWithoutMatches(); // Sẽ sử dụng factory
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


    // ProcessSwapAndMatchesCoroutine: được gọi bởi ProcessingMoveState
    public IEnumerator ProcessSwapAndMatchesCoroutine(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log($"=== ProcessSwapAndMatchesCoroutine START: {firstCandy.name} with {secondCandy.name} ===");

        DoSwap(firstCandy, secondCandy); // Thực hiện swap và animation
        yield return new WaitForSeconds(0.3f); // Chờ animation swap

        bool hasMatch = CheckBoard(); // CheckBoard populates this.candyToRemove
        Debug.Log($"CheckBoard result after swap: {hasMatch}");

        if (hasMatch)
        {
            Debug.Log("Match found after swap! Processing matches via ProcessTurnOnMatchedBoard...");
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(true)); // true: trừ lượt đi
            // Sau khi ProcessTurnOnMatchedBoard hoàn tất (bao gồm cả cascade)
        }
        else
        {
            Debug.Log("No match found from swap, swapping back.");
            DoSwap(firstCandy, secondCandy); // Swap lại (secondCandy giờ ở vị trí của firstCandy và ngược lại)
            yield return new WaitForSeconds(0.3f);
        }
        if (!hasMatch) // Chỉ gọi nếu không có match nào được xử lý bởi ProcessTurnOnMatchedBoard
        {
            FinalizeCurrentTurnProcessing();
        }
        Debug.Log("=== ProcessSwapAndMatchesCoroutine END ===");
    }


    // ProcessTurnOnMatchedBoard giờ đây sẽ gọi FinalizeCurrentTurnProcessing khi nó hoàn thành tất cả các cascade.
    public IEnumerator ProcessTurnOnMatchedBoard(bool subtractMoves)
    {
        if (this.candyToRemove.Count == 0)
        {
            FinalizeCurrentTurnProcessing(); // Gọi nếu không có kẹo nào để xóa ngay từ đầu
            yield break;
        }

        List<Candy> initialMatches = new List<Candy>(this.candyToRemove); // candyToRemove được CheckBoard() populate
        HashSet<Candy> allDestroyedThisTurn = RemoveAndRefill(initialMatches);

        if (allDestroyedThisTurn.Count > 0 && GameManager.instance != null)
        {
            GameManager.instance.ProcessTurn(allDestroyedThisTurn.Count, subtractMoves);
        }

        this.candyToRemove.Clear();
        yield return new WaitForSeconds(0.4f); // Delay cho animations và effects

        if (CheckBoard()) // Kiểm tra các match mới do refill
        {
            // Đệ quy để xử lý cascade, không trừ lượt đi cho cascade
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(false));
        }
        else
        {
            // Không còn match nào sau cascade (hoặc không có cascade)
            yield return new WaitForSeconds(0.2f);
            FinalizeCurrentTurnProcessing();
        }
    }

    // Phương thức được gọi khi một lượt xử lý (swap, match, cascade) hoàn tất
    public void FinalizeCurrentTurnProcessing()
    {
        Debug.Log("Finalizing current turn processing.");
        DeselectCurrentCandy(); // Dọn dẹp selected candy nếu có
        if (!CheckForPossibleMatches() && !(currentState is InitializingBoardState)) // Tránh vòng lặp vô hạn khi khởi tạo
        {
            Debug.Log("No more possible matches after turn, setting NoPossibleMovesState.");
            SetState(new NoPossibleMovesState(this));
        }
        else if (!(currentState is InitializingBoardState)) // Nếu đang khởi tạo, InitializeBoardCoroutineInternal sẽ xử lý
        {
            Debug.Log("Turn processing complete, moves available, setting IdleState.");
            SetState(new IdleState(this));
        }
        // Nếu đang trong InitializingBoardState, InitializeBoardCoroutineInternal sẽ gọi FinalizeBoardInitialization
        // để quyết định state tiếp theo.
    }

    public IEnumerator HandleNoPossibleMovesCoroutine()
    {
        Debug.Log("HandleNoPossibleMovesCoroutine: No possible moves. For now, re-initializing board after delay.");
        yield return new WaitForSeconds(1.5f); // Chờ một chút trước khi thử khởi tạo lại
        SetState(new InitializingBoardState(this)); // Tạm thời khởi tạo lại
    }

    private void CreateBoardWithoutMatches()
    {
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                Vector3 position = new Vector3((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale, 0);
                if (arrayLayout.rows[y].row[x]) // Vị trí bị chặn
                {
                    candyBoard[x, y] = new Node(false, null);
                }
                else
                {
                    List<int> availableTypes = GetAvailableCandyTypes(x, y);
                    if (availableTypes.Count == 0) // Nếu không có type nào để tránh match, lấy bất kỳ
                    {
                        availableTypes = new List<int>();
                        for (int i = 0; i < this.candyPrefabs.Length; i++) availableTypes.Add(i);
                    }
                    int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];
                    CandyType typeToCreate = (CandyType)randomIndex;

                    // THAY ĐỔI: Sử dụng CandyFactory
                    Candy newCandy = _candyFactory.CreateRegularCandy(typeToCreate, x, y, position);

                    if (newCandy != null)
                    {
                        candyBoard[x, y] = new Node(true, newCandy.gameObject);
                        candyToDestroy.Add(newCandy.gameObject); // Vẫn thêm GO vào list này để ClearEntireBoard
                        // Debug.Log($"Created candy via factory at [{x},{y}] type {newCandy.candyType}");
                    }
                    else
                    {
                        Debug.LogError($"Failed to create candy via factory at [{x},{y}] for type {typeToCreate}. Board position will be empty or bugged.");
                        candyBoard[x, y] = new Node(true, null); // Đánh dấu là usable nhưng null candy
                    }
                }
            }
        }
    }

    private List<int> GetAvailableCandyTypes(int x, int y)
    {
        List<int> availableTypes = new List<int>();
        for (int i = 0; i < candyPrefabs.Length; i++)
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
        Debug.Log("ClearEntireBoard: Initiating board clear.");

        if (_candyFactory != null)
        {
            if (candyBoard != null)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    for (int y = 0; y < boardHeight; y++)
                    {
                        if (candyBoard[x, y] != null && candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
                        {
                            Candy candyComponent = candyBoard[x, y].candy.GetComponent<Candy>();
                            if (candyComponent != null)
                            {
                                _candyFactory.ReturnCandyToPool(candyComponent);
                            }
                            else
                            {
                                // Nếu không có component Candy, đó là một GameObject lạ, hủy nó.
                                Debug.LogWarning($"ClearEntireBoard: Destroying unknown GameObject '{candyBoard[x, y].candy.name}' at [{x},{y}].");
                                Destroy(candyBoard[x, y].candy);
                            }
                            candyBoard[x, y].candy = null; // Quan trọng: Dọn dẹp tham chiếu trên board
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogError("ClearEntireBoard: _candyFactory is null! Cannot return candies to pool. Falling back to destroying children.");
            // Fallback nếu factory null (không nên xảy ra)
            if (candyParent != null)
            {
                foreach (Transform child in candyParent.transform) Destroy(child.gameObject);
            }
        }

        // Dọn dẹp các danh sách
        candyToDestroy?.Clear(); // List này có thể không còn cần thiết nữa.
        candyToRemove?.Clear();

        DeselectCurrentCandy();
        isProcessingMove = false; // Đảm bảo reset trạng thái này
        Debug.Log("ClearEntireBoard: Board cleared and candies returned to pool (if applicable).");
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
    private HashSet<Candy> RemoveAndRefill(List<Candy> initialMatches)
    {
        HashSet<Candy> allCandiesToDestroySet = new HashSet<Candy>(); // Những kẹo sẽ bị loại bỏ trong lượt này
        Queue<Candy> processQueue = new Queue<Candy>(); // Hàng đợi xử lý kẹo (đặc biệt là kẹo special)
        List<Candy> specialCandiesActivatedThisCycle = new List<Candy>(); // Tránh kích hoạt lặp lại kẹo special

        // Thêm các kẹo từ initialMatches vào hàng đợi xử lý
        foreach (Candy candy in initialMatches)
        {
            if (candy != null && candy.gameObject.activeSelf && allCandiesToDestroySet.Add(candy))
            {
                processQueue.Enqueue(candy);
                candy.isMatched = true; // Đánh dấu đã khớp
            }
        }

        // Xử lý hiệu ứng của kẹo special trong hàng đợi
        while (processQueue.Count > 0)
        {
            Candy currentCandy = processQueue.Dequeue();
            if (currentCandy == null || !currentCandy.gameObject.activeSelf) continue;

            if (currentCandy.isSpecial &&
                !specialCandiesActivatedThisCycle.Contains(currentCandy)) // Chỉ kích hoạt nếu chưa làm trong chu kỳ này
            {
                // Debug.Log($"RemoveAndRefill: Processing special effect for {currentCandy.name}.");
                specialCandiesActivatedThisCycle.Add(currentCandy);

                // ExecuteSpecialEffectLogic sẽ cập nhật allCandiesToDestroySet và trả về các kẹo *mới* bị ảnh hưởng
                List<Candy> newlyAffectedBySpecial = currentCandy.ExecuteSpecialEffectLogic(this, allCandiesToDestroySet);

                foreach (Candy newlyHitCandy in newlyAffectedBySpecial)
                {
                    if (newlyHitCandy != null && newlyHitCandy.gameObject.activeSelf && allCandiesToDestroySet.Add(newlyHitCandy))
                    {
                        newlyHitCandy.isMatched = true; // Đảm bảo đánh dấu
                        if (newlyHitCandy.isSpecial && !processQueue.Contains(newlyHitCandy) && !specialCandiesActivatedThisCycle.Contains(newlyHitCandy))
                        {
                            // Debug.Log($"RemoveAndRefill: Queuing chained special candy: {newlyHitCandy.name}");
                            processQueue.Enqueue(newlyHitCandy);
                        }
                    }
                }
            }
        }

        CreateSpecialCandyIfMatch(initialMatches, allCandiesToDestroySet);

        HashSet<int> columnsToRefill = new HashSet<int>();
        if (allCandiesToDestroySet.Count > 0)
        {
            // Debug.Log($"RemoveAndRefill: Candies marked for destruction: {allCandiesToDestroySet.Count}");
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
                // Dù thế nào, kẹo trong allCandiesToDestroySet cũng cần được trả về pool.
                _candyFactory.ReturnCandyToPool(candyToReturn);
            }
        }

        // Thu gọn cột và lấp đầy khoảng trống
        foreach (int x in columnsToRefill.Distinct()) // Đảm bảo không xử lý trùng cột
        {
            CollapseColumn(x);
            FillEmptySpacesInColumn(x);
        }
        return allCandiesToDestroySet; // Trả về tập hợp các kẹo đã bị phá hủy (đã được trả về pool)
    }

    private void CreateSpecialCandyIfMatch(List<Candy> matchedCandiesFromOriginalMatch, HashSet<Candy> allCandiesCurrentlyBeingDestroyed)
    {
        if (matchedCandiesFromOriginalMatch == null || matchedCandiesFromOriginalMatch.Count < 4) return;

        Candy primaryCandy = null; // Kẹo làm mốc để quyết định vị trí và loại kẹo đặc biệt
        Candy currentSelected = GetSelectedCandy(); // Giả sử bạn có hàm này
        if (currentSelected != null && matchedCandiesFromOriginalMatch.Contains(currentSelected))
        {
            primaryCandy = currentSelected;
        }
        else
        {
            List<Candy> sortedMatch = matchedCandiesFromOriginalMatch.OrderBy(c => c.xIndex).ThenBy(c => c.yIndex).ToList();
            if (sortedMatch.Any())
            {
                primaryCandy = sortedMatch[sortedMatch.Count / 2]; // Kẹo ở giữa
            }
        }

        if (primaryCandy == null || !primaryCandy.gameObject.activeSelf) // Nếu không tìm được kẹo gốc hoặc nó đã inactive
        {
            // Debug.LogWarning("CreateSpecialCandyIfMatch: Could not determine a valid primary candy for special creation.");
            return;
        }


        int specialX = primaryCandy.xIndex;
        int specialY = primaryCandy.yIndex;
        CandyType originalType = primaryCandy.candyType;
        // Vị trí tạo kẹo đặc biệt, không phải vị trí transform hiện tại của primaryCandy (vì nó có thể đang di chuyển)
        Vector3 specialPosition = new Vector3((specialX - spaceingX) * spacingScale, (specialY - spaceingY) * spacingScale, primaryCandy.transform.position.z);


        // Kiểm tra hướng match (sử dụng matchedCandiesFromOriginalMatch vì đây là match gốc)
        bool isHorizontalMatch = true;
        int firstY = matchedCandiesFromOriginalMatch[0].yIndex;
        foreach (Candy c in matchedCandiesFromOriginalMatch) if (c.yIndex != firstY) { isHorizontalMatch = false; break; }

        bool isVerticalMatch = true;
        int firstX = matchedCandiesFromOriginalMatch[0].xIndex;
        foreach (Candy c in matchedCandiesFromOriginalMatch) if (c.xIndex != firstX) { isVerticalMatch = false; break; }

        Candy newSpecialCandy = null;
        SpecialCandyEffect effectToCreate = SpecialCandyEffect.None;

        if (matchedCandiesFromOriginalMatch.Count >= 4) // Chỉ tạo cho match từ 4 trở lên
        {
            // Ưu tiên match dài hơn hoặc theo logic cụ thể của game bạn
            if (isHorizontalMatch && !isVerticalMatch) // Chỉ ngang
            {
                effectToCreate = SpecialCandyEffect.ClearRow;
            }
            else if (isVerticalMatch && !isHorizontalMatch) // Chỉ dọc
            {
                effectToCreate = SpecialCandyEffect.ClearColumn;
            }
            else if (isHorizontalMatch && isVerticalMatch) // Có thể là L-shape, T-shape hoặc 5+ cross
            {
                if (matchedCandiesFromOriginalMatch.Count(c => c.xIndex == specialX) > matchedCandiesFromOriginalMatch.Count(c => c.yIndex == specialY) && isVerticalMatch)
                {
                    effectToCreate = SpecialCandyEffect.ClearColumn; // Nếu cột dài hơn trong L/T
                }
                else if (isHorizontalMatch)
                {
                    effectToCreate = SpecialCandyEffect.ClearRow;
                }
                else if (isVerticalMatch)
                {
                    effectToCreate = SpecialCandyEffect.ClearColumn;
                }
            }
        }

        if (effectToCreate != SpecialCandyEffect.None)
        {
            newSpecialCandy = _candyFactory.CreateSpecialCandy(originalType, effectToCreate, specialX, specialY, specialPosition);
        }

        if (newSpecialCandy != null)
        {
            if (candyBoard[specialX, specialY].candy == primaryCandy.gameObject || candyBoard[specialX, specialY].candy == null)
            {
                candyBoard[specialX, specialY].candy = newSpecialCandy.gameObject;
            }
            else
            {
                Debug.LogWarning($"CreateSpecialCandyIfMatch: Board at [{specialX},{specialY}] was unexpectedly occupied by {candyBoard[specialX, specialY].candy?.name}. This might be an issue. Overwriting.");
                Candy existingCandy = candyBoard[specialX, specialY].candy?.GetComponent<Candy>();
                if (existingCandy != null && existingCandy != newSpecialCandy)
                { // Tránh tự trả về pool
                    _candyFactory.ReturnCandyToPool(existingCandy);
                }
                candyBoard[specialX, specialY].candy = newSpecialCandy.gameObject;
            }
        }
    }
    public void ClearBoardForReinitialization()
    {
        Debug.Log("--- ClearBoardForReinitialization: Starting board cleanup ---");

        DeselectCurrentCandy();

        // 3. Trả tất cả kẹo trên bảng logic (candyBoard[,]) về pool
        if (candyBoard != null && _candyFactory != null)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    Node node = candyBoard[x, y];
                    // Kiểm tra node có tồn tại và có kẹo không
                    if (node != null && node.candy != null)
                    {
                        Candy candyComponent = node.candy.GetComponent<Candy>();
                        if (candyComponent != null)
                        {
                            // Debug.Log($"ClearBoardForReinitialization: Returning candy '{candyComponent.name}' at [{x},{y}] to pool.");
                            _candyFactory.ReturnCandyToPool(candyComponent);
                        }
                        else
                        {
                            Debug.LogWarning($"ClearBoardForReinitialization: Destroying unknown GameObject '{node.candy.name}' at [{x},{y}] as it lacks Candy component.");
                            Destroy(node.candy);
                        }
                        node.candy = null; // Rất quan trọng: Xóa tham chiếu khỏi mảng board logic
                    }
                    // Nếu node không usable, vẫn nên đảm bảo node.candy là null
                    else if (node != null && !node.isUsable)
                    {
                        node.candy = null;
                    }
                }
            }
            Debug.Log("ClearBoardForReinitialization: All candies from logic board returned to pool and references nullified.");
        }
        else
        {
            Debug.LogWarning("ClearBoardForReinitialization: candyBoard array or _candyFactory is null. Full cleanup might not be possible.");
        }
        if (candyParent != null && _candyFactory != null)
        {
            List<GameObject> childrenToProcess = new List<GameObject>();
            foreach (Transform child in candyParent.transform)
            {
                childrenToProcess.Add(child.gameObject);
            }

            foreach (GameObject childGO in childrenToProcess)
            {
                if (childGO == null) continue;

                // Nếu GameObject đang active, có thể nó là kẹo "đi lạc"
                if (childGO.activeSelf)
                {
                    Candy orphanedCandy = childGO.GetComponent<Candy>();
                    if (orphanedCandy != null)
                    {
                        // Debug.LogWarning($"ClearBoardForReinitialization: Found active orphaned candy '{orphanedCandy.name}'. Returning to pool.");
                        _candyFactory.ReturnCandyToPool(orphanedCandy);
                    }
                    else
                    {
                        // Debug.LogWarning($"ClearBoardForReinitialization: Found active orphaned GameObject '{childGO.name}' without Candy component. Destroying.");
                        Destroy(childGO);
                    }
                }
                // Nếu GameObject không active, nó có thể là một phần của pool, không đụng vào.
            }
        }
        candyToRemove.Clear(); // Danh sách kẹo cần xóa trong một lượt match

        Debug.Log("--- ClearBoardForReinitialization: Board cleanup finished. ---");
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
                List<int> availableTypes = GetAvailableCandyTypes(x, y); // Đảm bảo không tạo match mới ngay
                if (availableTypes.Count == 0)
                {
                    availableTypes = new List<int>();
                    for (int i = 0; i < this.candyPrefabs.Length; i++) availableTypes.Add(i);
                }
                int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];
                CandyType typeToCreate = (CandyType)randomIndex;

                // Vị trí spawn kẹo (thường là ở trên cùng cột, ngoài màn hình)
                Vector3 spawnPos = new Vector3(
                    (x - spaceingX) * spacingScale,
                    (boardHeight - spaceingY) * spacingScale, // Spawn ở vị trí Y cao nhất của board (hoặc cao hơn)
                    0);
                // Vị trí đích của kẹo
                Vector3 targetPos = new Vector3(
                    (x - spaceingX) * spacingScale,
                    (y - spaceingY) * spacingScale,
                    0);

                // THAY ĐỔI: Sử dụng CandyFactory (Instantiate tại spawnPos)
                Candy newCandy = _candyFactory.CreateRegularCandy(typeToCreate, x, y, spawnPos);

                if (newCandy != null)
                {
                    newCandy.MoveToTarget(targetPos); // Di chuyển kẹo đến vị trí đích
                    candyBoard[x, y] = new Node(true, newCandy.gameObject);
                    candyToDestroy.Add(newCandy.gameObject);
                    // Debug.Log($"Filled empty space at [{x},{y}] with new candy {newCandy.candyType}");
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
                    SetState(new InitializingBoardState(this)); // This will set isProcessingMove internally if it runs
                }
            }
        }
        Debug.Log("=== ProcessMatches END (or handing over to ProcessTurnOnMatchedBoard) ===");
    }
    public void ReportCandyClicked(Candy candy)
    {
        if (currentState != null && candy != null && !candy.isMoving) // Thêm điều kiện !candy.isMoving
        {
            currentState.HandleCandyClick(candy);
        }
        else if (candy != null && candy.isMoving)
        {
            Debug.Log($"Candy {candy.name} is moving. Click ignored by CandyBoard.");
        }
        else
        {
            Debug.LogWarning("ReportCandyClicked: currentState is null or candy is null.");
        }
    }
    public bool IsAdjacent(Candy firstCandy, Candy secondCandy)
    {
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("IsAdjacent: One or both candies are null");
            return false;
        }
        bool adjacent = Mathf.Abs(firstCandy.xIndex - secondCandy.xIndex) +
                        Mathf.Abs(firstCandy.yIndex - secondCandy.yIndex) == 1;
        // Debug.Log($"IsAdjacent check: [{firstCandy.xIndex},{firstCandy.yIndex}] to [{secondCandy.xIndex},{secondCandy.yIndex}] = {adjacent}");
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