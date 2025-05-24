// GameManager.cs - Phiên bản cập nhật
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Game Panels")]
    public GameObject backgroundPanel;
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    [Header("UI Text")]
    public TMP_Text pointText;
    public TMP_Text movesText;
    public TMP_Text goalText;

    [Header("Level Settings")] // THÊM MỚI: Dữ liệu cấu hình cho level
    public int levelMoves = 20;
    public int levelGoal = 5000;

    [Header("Game State")]
    public int goal;
    public int moves;
    public int scores;
    public bool isGameOver;

    [Header("Test Mode")]
    [Tooltip("Kích hoạt để có 9999 moves và 999999 goal")]
    public bool isTestMode = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnScoreChanged.AddListener(UpdateScoreText);
        GameEvents.OnMovesChanged.AddListener(UpdateMovesText);
        GameEvents.OnGoalChanged.AddListener(UpdateGoalText);
        GameEvents.OnGameWin.AddListener(HandleWinCondition);
        GameEvents.OnGameLose.AddListener(HandleLoseCondition);
    }

    private void OnDisable()
    {
        GameEvents.OnScoreChanged.RemoveListener(UpdateScoreText);
        GameEvents.OnMovesChanged.RemoveListener(UpdateMovesText);
        GameEvents.OnGoalChanged.RemoveListener(UpdateGoalText);
        GameEvents.OnGameWin.RemoveListener(HandleWinCondition);
        GameEvents.OnGameLose.RemoveListener(HandleLoseCondition);
    }

    // THAY ĐỔI: Hàm Initialize giờ không cần tham số
    // Nó sẽ tự lấy giá trị từ cấu hình của level
    public void Initialize()
    {
        if (isTestMode)
        {
            this.moves = 9999;
            this.goal = 999999;
            Debug.LogWarning("--- TEST MODE ACTIVATED ---");
        }
        else
        {
            this.moves = levelMoves; // Lấy từ cấu hình
            this.goal = levelGoal;   // Lấy từ cấu hình
        }

        this.scores = 0;
        this.isGameOver = false;

        // Phát sự kiện để cập nhật UI ngay từ đầu -> ĐÂY LÀ CHÌA KHÓA
        Debug.Log($"Initializing game with: Moves={moves}, Goal={goal}, Scores={scores}. Firing initial events.");
        GameEvents.OnGoalChanged.Invoke(goal);
        GameEvents.OnMovesChanged.Invoke(moves);
        GameEvents.OnScoreChanged.Invoke(scores);
    }

    // ... (Các phần còn lại của GameManager giữ nguyên)
    public void ProcessTurn(int pointToGain, bool subtractMoves)
    {
        if (isGameOver) return;

        scores += pointToGain;
        GameEvents.OnScoreChanged.Invoke(scores);

        if (subtractMoves)
        {
            moves--;
            GameEvents.OnMovesChanged.Invoke(moves);
        }

        if (scores >= goal)
        {
            isGameOver = true;
            GameEvents.OnGameWin.Invoke();
            return;
        }

        if (moves <= 0)
        {
            isGameOver = true;
            GameEvents.OnGameLose.Invoke();
        }
    }

    private void HandleWinCondition()
    {
        Debug.Log("Game Won!");
        backgroundPanel.SetActive(true);
        victoryPanel.SetActive(true);
        if (CandyBoard.instance != null && CandyBoard.instance.candyParent != null)
        {
            CandyBoard.instance.candyParent.SetActive(false);
        }
    }

    private void HandleLoseCondition()
    {
        Debug.Log("Game Lost!");
        backgroundPanel.SetActive(true);
        defeatPanel.SetActive(true);
        if (CandyBoard.instance != null && CandyBoard.instance.candyParent != null)
        {
            CandyBoard.instance.candyParent.SetActive(false);
        }
    }

    #region UI Update Methods
    private void UpdateScoreText(int newScore)
    {
        pointText.text = "Points: " + newScore.ToString();
    }

    private void UpdateMovesText(int newMoves)
    {
        movesText.text = "Moves: " + newMoves.ToString();
    }

    private void UpdateGoalText(int newGoal)
    {
        goalText.text = "Goal: " + newGoal.ToString();
    }
    #endregion

    #region Scene Management
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    #endregion
}