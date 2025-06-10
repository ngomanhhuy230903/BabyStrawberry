using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Yêu cầu các component phụ thuộc phải có mặt trên cùng GameObject
// để tránh lỗi quên gán trong Inspector.
[RequireComponent(typeof(InputController), typeof(HintController), typeof(BoardProcessor))]
public class CandyBoard : MonoBehaviour
{
    #region Public Fields & Properties
    [Header("Board Dimensions")]
    public int boardWidth = 6;
    public int boardHeight = 8;

    [Header("Board Layout & Prefabs")]
    public ArrayLayout arrayLayout;
    public GameObject candyParent;
    public GameObject[] candyPrefabs;
    public GameObject[] rowClearerPrefabs;
    public GameObject[] columnClearerPrefabs;
    public GameObject colorBombPrefab;

    [Header("Pooling Settings")]
    public int initialPoolSizePerType = 30;

    // Các thuộc tính có thể được truy cập bởi các hệ thống con
    public float spaceingX { get; private set; }
    public float spaceingY { get; private set; }
    public float spacingScale = 1.5f;
    public Node[,] candyBoard;
    public List<Candy> candyToRemove = new List<Candy>();
    #endregion

    #region Private References
    // Singleton instance
    public static CandyBoard instance;

    // State machine
    private IBoardState currentState;
    private Candy _selectedCandy;

    // Tham chiếu đến các hệ thống con
    private InputController _inputController;
    private HintController _hintController;
    private BoardProcessor _boardProcessor;
    private CandyFactory _candyFactory;
    private BoardMatcher _boardMatcher;
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        instance = this;

        // Lấy tham chiếu đến các component trên cùng GameObject
        _inputController = GetComponent<InputController>();
        _hintController = GetComponent<HintController>();
        _boardProcessor = GetComponent<BoardProcessor>();

        // Khởi tạo các class C# thuần túy
        _candyFactory = new CandyFactory(candyPrefabs, rowClearerPrefabs, columnClearerPrefabs, colorBombPrefab, candyParent.transform, initialPoolSizePerType);
        _boardMatcher = new BoardMatcher(boardWidth, boardHeight);

        // Cung cấp các dependency cho các hệ thống con (Dependency Injection)
        _hintController.Initialize(this, _boardMatcher);
        _boardProcessor.Initialize(this, _candyFactory, _boardMatcher);
    }

    private void Start()
    {
        // Đăng ký lắng nghe các sự kiện từ InputController
        _inputController.OnCandyClicked += HandleCandyClickFromInput;
        _inputController.OnResetBoardPressed += HandleDebugReset;
        _inputController.OnShowStatusPressed += HandleDebugStatus;
        _inputController.OnFindHintPressed += HandleDebugHint;

        SetState(new InitializingBoardState(this));
    }

    private void OnDestroy()
    {
        // Luôn hủy đăng ký sự kiện để tránh memory leak
        if (_inputController != null)
        {
            _inputController.OnCandyClicked -= HandleCandyClickFromInput;
            _inputController.OnResetBoardPressed -= HandleDebugReset;
            _inputController.OnShowStatusPressed -= HandleDebugStatus;
            _inputController.OnFindHintPressed -= HandleDebugHint;
        }
    }

    // Update() bây giờ siêu gọn gàng, chỉ ủy quyền cho state hiện tại
    private void Update()
    {
        currentState?.UpdateState();
    }
    #endregion

    #region State Management
    public void SetState(IBoardState newState)
    {
        currentState?.OnExit();

        _hintController.ResetIdleTimer(); // Luôn reset hint khi chuyển state

        currentState = newState;
        currentState?.OnEnter();

        if (currentState is IdleState)
        {
            _hintController.StartIdleTimer(); // Bắt đầu đếm giờ hint khi vào Idle
        }
    }
    #endregion

    #region Event Handlers
    private void HandleCandyClickFromInput(Candy candy)
    {
        if (currentState != null && candy != null && !candy.isMoving)
        {
            currentState.HandleCandyClick(candy);
        }
    }

    private void HandleDebugReset() => SetState(new InitializingBoardState(this));
    private void HandleDebugStatus() => Debug.Log($"Current State: {currentState?.GetType().Name}");
    private void HandleDebugHint()
    {
        var hint = _boardMatcher.FindHint(candyBoard);
        if (hint != null) Debug.Log($"Hint found: {hint[0].name} and {hint[1].name}");
        else Debug.Log("No hint found.");
    }
    #endregion

    #region Public Game Flow Methods
    public IEnumerator InitializeBoardCoroutineInternal()
    {
        Debug.Log("Initializing board...");
        DeselectCurrentCandy();
        ClearEntireBoard();

        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)(boardWidth - 1) / 2;
        spaceingY = (float)(boardHeight - 1) / 2;

        CreateBoardWithoutMatches();
        yield return new WaitForSeconds(0.1f);

        if (_boardMatcher.FindAllMatches(candyBoard, candyToRemove))
        {
            Debug.Log("Initial matches found, processing...");
            _boardProcessor.StartProcessingTurn(false, new List<Candy>(candyToRemove));
        }
        else
        {
            FinalizeCurrentTurnProcessing();
        }
    }

    public IEnumerator ProcessSwapAndMatchesCoroutine(Candy firstCandy, Candy secondCandy)
    {
        Debug.Log($"Processing swap: {firstCandy.name} with {secondCandy.name}");

        bool isColorBombSwap = firstCandy.specialEffect == SpecialCandyEffect.ClearColor || secondCandy.specialEffect == SpecialCandyEffect.ClearColor;
        if (isColorBombSwap)
        {
            _boardProcessor.StartProcessingTurn(true, new List<Candy>(), firstCandy, secondCandy);
            yield break;
        }

        DoSwap(firstCandy, secondCandy);
        yield return new WaitForSeconds(0.3f);

        if (_boardMatcher.FindAllMatches(candyBoard, candyToRemove))
        {
            _boardProcessor.StartProcessingTurn(true, new List<Candy>(candyToRemove), firstCandy);
        }
        else
        {
            // Swap không hợp lệ, đổi lại
            DoSwap(firstCandy, secondCandy);
            yield return new WaitForSeconds(0.3f);
            FinalizeCurrentTurnProcessing();
        }
    }

    public void FinalizeCurrentTurnProcessing()
    {
        Debug.Log("Finalizing turn.");
        DeselectCurrentCandy();

        if (GameManager.instance.isGameOver)
        {
            // Xử lý logic khi game over nếu cần
            return;
        }

        if (!_boardMatcher.HasPossibleMoves(candyBoard))
        {
            Debug.Log("No more possible moves!");
            SetState(new NoPossibleMovesState(this));
        }
        else
        {
            SetState(new IdleState(this));
        }
    }

    public IEnumerator HandleNoPossibleMovesCoroutine()
    {
        Debug.Log("Resetting board due to no moves...");
        yield return new WaitForSeconds(1.5f);
        SetState(new InitializingBoardState(this));
    }
    #endregion

    #region Helper Methods
    // Thêm hàm này vào file CandyBoard.cs
    public IEnumerator WaitForCandiesToSettle()
    {
        // Chờ một frame để các coroutine MoveToCoroutine có thể bắt đầu và set isMoving = true
        yield return new WaitForEndOfFrame();

        bool allCandiesSettled = false;
        while (!allCandiesSettled)
        {
            allCandiesSettled = true;
            for (int y = 0; y < boardHeight; y++)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
                    {
                        if (candyBoard[x, y].candy.GetComponent<Candy>().isMoving)
                        {
                            allCandiesSettled = false;
                            break; // Thoát vòng lặp trong
                        }
                    }
                }
                if (!allCandiesSettled)
                {
                    break; // Thoát vòng lặp ngoài
                }
            }
            yield return null; // Chờ đến frame tiếp theo để kiểm tra lại
        }
    }
    public void DoSwap(Candy firstCandy, Candy secondCandy)
    {
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

    public bool IsAdjacent(Candy c1, Candy c2) => Mathf.Abs(c1.xIndex - c2.xIndex) + Mathf.Abs(c1.yIndex - c2.yIndex) == 1;
    public Candy GetSelectedCandy() => _selectedCandy;
    public CandyFactory GetCandyFactory() => _candyFactory;
    public void SetSelectedCandy(Candy candy) => _selectedCandy = candy;
    public void DeselectCurrentCandy()
    {
        if (_selectedCandy != null)
        {
            _selectedCandy.SetSelected(false);
            _selectedCandy = null;
        }
    }

    private void CreateBoardWithoutMatches()
    {
        // (Logic tạo bảng từ file gốc của bạn, nhưng sử dụng _boardMatcher để kiểm tra)
        // Đây là ví dụ đơn giản hóa, bạn có thể copy logic GetAvailableCandyTypes vào đây
        // hoặc để nó trong BoardProcessor và gọi thông qua tham chiếu nếu cần.
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                if (!arrayLayout.rows[y].row[x])
                {
                    Vector3 position = new Vector3((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale, 0);
                    int randomIndex = Random.Range(0, candyPrefabs.Length);
                    Candy newCandy = _candyFactory.CreateRegularCandy((CandyType)randomIndex, x, y, position);
                    candyBoard[x, y] = new Node(true, newCandy.gameObject);
                }
                else
                {
                    candyBoard[x, y] = new Node(false, null);
                }
            }
        }
        // Thêm một bước kiểm tra để đảm bảo không có match khi khởi tạo
    }

    public void ClearEntireBoard()
    {
        if (_candyFactory == null || candyBoard == null) return;
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (candyBoard[x, y]?.isUsable == true && candyBoard[x, y]?.candy != null)
                {
                    _candyFactory.ReturnCandyToPool(candyBoard[x, y].candy.GetComponent<Candy>());
                    candyBoard[x, y].candy = null;
                }
            }
        }
    }
    #endregion
    // Thêm đoạn code này vào bên trong file CandyBoard.cs

    #region Timer Passthrough Methods
    /// <summary>
    /// Hàm công khai để các State có thể yêu cầu bắt đầu đếm giờ.
    /// CandyBoard sẽ truyền lệnh này đến HintController.
    /// </summary>
    public void StartIdleTimer()
    {
        // Dấu '?' để đảm bảo không bị lỗi nếu _hintController chưa được gán.
        _hintController?.StartIdleTimer();
    }

    /// <summary>
    /// Hàm công khai để các State có thể yêu cầu reset bộ đếm giờ.
    /// CandyBoard sẽ truyền lệnh này đến HintController.
    /// </summary>
    public void ResetIdleTimer()
    {
        _hintController?.ResetIdleTimer();
    }
    #endregion
}
