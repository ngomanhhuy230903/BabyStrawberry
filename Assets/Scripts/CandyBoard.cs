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
    [SerializeField] private Candy _selectedCandy; // Giữ lại để các state có thể truy cập nếu cần, hoặc truyền qua constructor state

    private IBoardState currentState; // THAY ĐỔI: Trạng thái hiện tại của board

    public void Awake()
    {
        instance = this;
        ValidateSpecialPrefabs();
    }
    public void Start()
    {
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

    public void Update()
    {
        // Xử lý input chung ở đây, sau đó delegate cho state
        if (Input.GetMouseButtonDown(0))
        {
            // Chỉ xử lý click nếu state hiện tại cho phép (ví dụ, không phải đang processing)
            // Việc quyết định có xử lý click hay không sẽ do state hiện tại đảm nhiệm
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

    public IEnumerator InitializeBoardCoroutineInternal() // Đổi tên để phân biệt
    {
        Debug.Log("Initializing board (Internal Coroutine)...");
        DeselectCurrentCandy();
        ClearEntireBoard(); // Xóa kẹo cũ (nếu có)

        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2) + 1;
        spaceingY = (float)((boardHeight - 1) / 2) - 1;

        // ... (Phần kiểm tra candyPrefab, arrayLayout không đổi) ...
        if (candyPrefab == null || candyPrefab.Length == 0) { /* ... */ yield break; }
        // ... (các vòng lặp kiểm tra prefab, sr, sprite ...)

        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight) { /* ... */ yield break; }
        // ... (vòng lặp kiểm tra arrayLayout.rows[y].row ...)


        CreateBoardWithoutMatches();
        yield return new WaitForSeconds(0.1f); // Giảm delay

        bool initialMatchesFound = CheckBoard(); // CheckBoard populates this.candyToRemove
        if (initialMatchesFound)
        {
            Debug.Log("Initial matches found, processing via ProcessTurnOnMatchedBoard (no move subtract)...");
            // ProcessTurnOnMatchedBoard sẽ tự xử lý và cuối cùng sẽ kiểm tra lại possible moves
            // và có thể tự chuyển state thông qua callback hoặc gọi phương thức trên CandyBoard
            yield return StartCoroutine(ProcessTurnOnMatchedBoard(false));
            // Sau khi ProcessTurnOnMatchedBoard hoàn tất (bao gồm cả cascade)
        }
        // Sau khi xử lý match ban đầu (nếu có) hoặc nếu không có match ban đầu:
        FinalizeBoardInitialization();
    }

    private void FinalizeBoardInitialization()
    {
        if (!CheckForPossibleMatches())
        {
            Debug.Log("No possible matches on the board after init/initial processing, re-triggering initialization...");
            // Thay vì gọi lại InitializeBoard() trực tiếp, chúng ta set state
            SetState(new InitializingBoardState(this)); // Điều này sẽ bắt đầu lại toàn bộ quá trình
        }
        else
        {
            Debug.Log("Board initialization complete with valid moves available.");
            SetState(new IdleState(this)); // Chuyển sang trạng thái sẵn sàng chơi
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
            // Kiểm tra xem có phải là swap 2 kẹo đặc biệt không tạo match 3 nhưng kích hoạt nhau không?
            // Logic này có thể phức tạp. Tạm thời, nếu không có match 3 thông thường, swap lại.
            Debug.Log("No match found from swap, swapping back.");
            DoSwap(firstCandy, secondCandy); // Swap lại (secondCandy giờ ở vị trí của firstCandy và ngược lại)
            yield return new WaitForSeconds(0.3f);
        }

        // Sau khi xử lý swap (dù thành công hay thất bại và swap lại):
        // Kiểm tra lại trạng thái bàn cờ và quyết định state tiếp theo.
        // ProcessTurnOnMatchedBoard sẽ tự gọi FinalizeCurrentTurnProcessing khi kết thúc.
        // Nếu không có match, chúng ta cần gọi nó ở đây.
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
            // Không có gì để xử lý, nhưng nếu hàm này được gọi, nó là một phần của một lượt xử lý lớn hơn.
            // Nếu đây là điểm cuối của một chuỗi xử lý, chúng ta cần hoàn tất lượt.
            // Tuy nhiên, thường thì nếu candyToRemove rỗng, nó sẽ không vào vòng lặp đệ quy.
            // Điểm kết thúc thực sự là khi CheckBoard() trả về false trong khối 'else' dưới đây.
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
        // Trong game thực tế, bạn có thể muốn shuffle board ở đây.
        // GameManager.instance.ShowShuffleAnimation();
        // ShuffleBoardLogically(); // Hàm này sẽ cố gắng tạo ra nước đi mới
        // yield return new WaitForSeconds(ShuffleAnimationDuration);
        // if (!CheckForPossibleMatches()) { /* Xử lý nếu shuffle vẫn không tạo ra nước đi */ }
        // else { SetState(new IdleState(this)); }

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
    private HashSet<Candy> RemoveAndRefill(List<Candy> initialMatches)
    {
        HashSet<Candy> allCandiesToDestroySet = new HashSet<Candy>();
        Queue<Candy> processQueue = new Queue<Candy>();
        // Theo dõi các kẹo đặc biệt đã kích hoạt trong CHU KỲ NÀY của RemoveAndRefill
        // để tránh kích hoạt lặp lại vô hạn nếu một kẹo đặc biệt tự xóa chính nó
        // hoặc bị xóa bởi một kẹo đặc biệt khác mà nó cũng xóa.
        List<Candy> specialCandiesActivatedThisCycle = new List<Candy>();

        foreach (Candy candy in initialMatches)
        {
            if (allCandiesToDestroySet.Add(candy)) // .Add trả về true nếu item mới được thêm vào set
            {
                processQueue.Enqueue(candy);
                candy.isMatched = true; // Đánh dấu là đã khớp
            }
        }

        while (processQueue.Count > 0)
        {
            Candy currentCandy = processQueue.Dequeue();

            // Kiểm tra xem kẹo này có phải là special và nên kích hoạt hiệu ứng không
            if (currentCandy.isSpecial &&
                allCandiesToDestroySet.Contains(currentCandy) && // Nó phải nằm trong danh sách bị hủy (do khớp hoặc do hiệu ứng khác)
                !specialCandiesActivatedThisCycle.Contains(currentCandy)) // Và chưa kích hoạt trong chu kỳ này
            {
                Debug.Log($"Processing special effect for {currentCandy.candyType} at [{currentCandy.xIndex},{currentCandy.yIndex}] using strategy.");
                specialCandiesActivatedThisCycle.Add(currentCandy); // Đánh dấu đã kích hoạt trong chu kỳ này

                // THAY ĐỔI CHÍNH: Gọi phương thức của Candy để thực thi logic qua strategy
                // ExecuteSpecialEffectLogic sẽ chạy cả visuals và logic của strategy
                // Strategy sẽ cập nhật allCandiesToDestroySet và trả về các kẹo *mới* bị ảnh hưởng
                List<Candy> newlyAffectedBySpecial = currentCandy.ExecuteSpecialEffectLogic(this, allCandiesToDestroySet);

                // Nếu các kẹo mới bị ảnh hưởng này cũng là special, thêm chúng vào hàng đợi
                // để xử lý hiệu ứng của chúng trong các vòng lặp tiếp theo của while này.
                foreach (Candy newlyHitCandy in newlyAffectedBySpecial)
                {
                    // Đảm bảo chúng được đánh dấu là isMatched để logic hủy hoạt động đúng
                    // (mặc dù allCandiesToDestroySet.Add đã làm điều này, nhưng để rõ ràng)
                    newlyHitCandy.isMatched = true;

                    // Chỉ thêm vào hàng đợi nếu nó là special, chưa có trong hàng đợi,
                    // và chưa kích hoạt trong chu kỳ này.
                    if (newlyHitCandy.isSpecial &&
                        !processQueue.Contains(newlyHitCandy) && // Tránh xử lý lại nếu đã có trong queue
                        !specialCandiesActivatedThisCycle.Contains(newlyHitCandy)) // Tránh kích hoạt lại trong cùng 1 cycle
                    {
                        Debug.Log($"Queuing chained special candy: {newlyHitCandy.name}");
                        processQueue.Enqueue(newlyHitCandy);
                    }
                }
            }
        }

        // Tạo kẹo đặc biệt mới dựa trên các kết hợp ban đầu (ví dụ: 4 kẹo thẳng hàng)
        // Điều này nên sử dụng danh sách 'initialMatches', không phải 'allCandiesToDestroySet',
        // để đảm bảo kẹo đặc biệt được tạo từ các kết hợp trực tiếp, không phải từ các vụ nổ.
        CreateSpecialCandyIfMatch(initialMatches); // Kiểm tra điều kiện tạo kẹo đặc biệt mới

        HashSet<int> columnsToRefill = new HashSet<int>();
        if (allCandiesToDestroySet.Count > 0)
        {
            foreach (Candy candy in allCandiesToDestroySet)
            {
                if (candy == null || candy.gameObject == null) continue; // Kẹo có thể đã bị hủy bởi một hiệu ứng khác

                int xIndex = candy.xIndex;
                int yIndex = candy.yIndex;
                columnsToRefill.Add(xIndex);

                // Đảm bảo kẹo đang bị hủy là kẹo trên bàn cờ tại tọa độ của nó,
                // hoặc xử lý các trường hợp nó có thể đã được thay thế (ví dụ: bởi CreateSpecialCandyIfMatch)
                if (candyBoard[xIndex, yIndex].candy == candy.gameObject)
                {
                    Destroy(candy.gameObject);
                    candyBoard[xIndex, yIndex] = new Node(true, null);
                }
                else if (candy.gameObject != null) // Nếu nó không trên bàn cờ nhưng tồn tại và trong set, hủy nó.
                {
                    // If the candy object still exists but is not what the board expects at its (old) location
                    // (e.g., it was replaced by a special candy, or already moved/destroyed by another effect),
                    // we still need to ensure this specific instance from allCandiesToDestroySet is destroyed.
                    Debug.LogWarning($"Candy {candy.name} at [{xIndex},{yIndex}] was in destroy set, but board has different/null candy. Destroying instance.");
                    Destroy(candy.gameObject);
                    // Không nullify node nếu không chắc chắn, vì node đó có thể đã chứa kẹo mới.
                    // Quan trọng nhất là đối tượng kẹo trong allCandiesToDestroySet bị hủy.
                    if (candyBoard[xIndex, yIndex].candy == candy.gameObject) // Kiểm tra lại nếu nó vô tình bị đặt lại
                    {
                        candyBoard[xIndex, yIndex] = new Node(true, null);
                    }
                }
            }
        }

        // Thu gọn cột và lấp đầy khoảng trống
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
                    SetState(new InitializingBoardState(this)); // This will set isProcessingMove internally if it runs
                }
            }
        }
        // isProcessingMove is generally set to false at the end of successful ProcessTurnOnMatchedBoard cascades
        // or explicitly in the swap-back case.
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