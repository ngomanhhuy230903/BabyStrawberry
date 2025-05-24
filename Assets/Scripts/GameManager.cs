// File: GameManager.cs (Đã cập nhật)
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public GameObject backgroundPanel;
    public GameObject victoryPanel;
    public GameObject defeatPanel;
    private bool _isGameOver;

    public TMP_Text pointText;
    public TMP_Text movesText;
    public TMP_Text goalText;
    private int _goal;
    public int goal
    {
        get => _goal;
        private set // Setter bây giờ sẽ private để kiểm soát việc thay đổi giá trị và phát sự kiện
        {
            if (_goal != value)
            {
                _goal = value;
                GameEvents.TriggerGoalChanged(_goal); // Phát sự kiện khi mục tiêu thay đổi
            }
        }
    }

    private int _moves;
    public int moves
    {
        get => _moves;
        private set
        {
            if (_moves != value)
            {
                _moves = value;
                GameEvents.TriggerMovesChanged(_moves); // Phát sự kiện khi lượt đi thay đổi
            }
        }
    }

    private int _scores;
    public int scores

    {
        get => _scores;
        private set
        {
            if (_scores != value)
            {
                _scores = value;
                GameEvents.TriggerScoreChanged(_scores); // Phát sự kiện khi điểm số thay đổi
            }
        }
    }


    public bool isGameOver // Giữ lại public getter, setter private để kiểm soát từ bên trong
    {
        get => _isGameOver;
        private set
        {
            // Không phát sự kiện OnGameOver trực tiếp từ setter này nữa
            // ProcessTurn sẽ quyết định và phát sự kiện với outcome cụ thể (Victory/Defeat)
            _isGameOver = value;
        }
    }


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad(gameObject); // Cân nhắc nếu GameManager cần tồn tại qua các scene
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Đăng ký lắng nghe các sự kiện để cập nhật UI
        GameEvents.OnScoreChanged += UpdateScoreUI;
        GameEvents.OnMovesChanged += UpdateMovesUI;
        GameEvents.OnGoalChanged += UpdateGoalUI;
        GameEvents.OnGameOver += HandleGameOverUI;
    }

    private void OnDestroy()
    {
        // Hủy đăng ký khi GameManager bị phá hủy để tránh lỗi
        GameEvents.OnScoreChanged -= UpdateScoreUI;
        GameEvents.OnMovesChanged -= UpdateMovesUI;
        GameEvents.OnGoalChanged -= UpdateGoalUI;
        GameEvents.OnGameOver -= HandleGameOverUI;
    }

    public void Initialize(int initialMoves, int initialGoal)
    {
        // Sử dụng setter để tự động phát sự kiện
        //this.moves = initialMoves;
        //this.goal = initialGoal;
        this.moves = 100;
        this.goal = 900000;
        this.scores = 0;
        this.isGameOver = false; // Reset trạng thái game over

        // Đảm bảo UI được cập nhật ngay khi Initialize, ngay cả khi giá trị không "thay đổi" so với giá trị mặc định
        GameEvents.TriggerMovesChanged(this.moves);
        GameEvents.TriggerGoalChanged(this.goal);
        GameEvents.TriggerScoreChanged(this.scores);


        if (backgroundPanel) backgroundPanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);

        if (CandyBoard.instance != null && CandyBoard.instance.candyParent != null)
        {
            CandyBoard.instance.candyParent.SetActive(true);
        }
    }

    // Xóa hàm Start() và Update() cho việc cập nhật UI, vì giờ đây chúng được điều khiển bởi sự kiện

    private void UpdateScoreUI(int newScore)
    {
        if (pointText) pointText.text = "Points: " + newScore.ToString();
    }

    private void UpdateMovesUI(int newMoves)
    {
        if (movesText) movesText.text = "Moves: " + newMoves.ToString();
    }

    private void UpdateGoalUI(int newGoal)
    {
        if (goalText) goalText.text = "Goal: " + newGoal.ToString();
    }

    private void HandleGameOverUI(GameEvents.GameOutcome outcome)
    {
        if (backgroundPanel) backgroundPanel.SetActive(true);
        if (CandyBoard.instance != null && CandyBoard.instance.candyParent != null)
        {
            CandyBoard.instance.candyParent.SetActive(false); // Ẩn bảng kẹo
        }

        if (outcome == GameEvents.GameOutcome.Victory)
        {
            if (victoryPanel) victoryPanel.SetActive(true);
            if (defeatPanel) defeatPanel.SetActive(false); // Đảm bảo panel thua ẩn
        }
        else // Defeat
        {
            if (defeatPanel) defeatPanel.SetActive(true);
            if (victoryPanel) victoryPanel.SetActive(false); // Đảm bảo panel thắng ẩn
        }
    }

    public void ProcessTurn(int pointToGain, bool subtractMoves)
    {
        if (isGameOver) return; // Không xử lý nếu game đã kết thúc

        scores += pointToGain; // Setter sẽ phát sự kiện OnScoreChanged

        if (subtractMoves)
        {
            moves--; // Setter sẽ phát sự kiện OnMovesChanged
        }

        // Kiểm tra điều kiện thắng trước
        if (scores >= goal)
        {
            isGameOver = true;
            GameEvents.TriggerGameOver(GameEvents.GameOutcome.Victory); // Phát sự kiện thắng
            // Hàm WinGame() sẽ được gọi bởi Button trên UI, hoặc có thể gọi trực tiếp ở đây nếu UI tách biệt hoàn toàn.
            return; // Quan trọng: thoát sau khi điều kiện thắng được đáp ứng
        }

        // Kiểm tra điều kiện thua (hết lượt đi và chưa thắng)
        if (moves <= 0)
        {
            isGameOver = true;
            GameEvents.TriggerGameOver(GameEvents.GameOutcome.Defeat); // Phát sự kiện thua
            // Hàm LoseGame() sẽ được gọi bởi Button trên UI.
            return; // Quan trọng: thoát sau khi điều kiện thua được đáp ứng
        }
    }

    // Các hàm này thường được gọi từ Button trên UI sau khi sự kiện OnGameOver đã hiển thị panel tương ứng.
    public void WinGame()
    {
        Debug.Log("WinGame action triggered - Loading Scene 0");
        SceneManager.LoadScene(0); // Hoặc scene menu chính/level tiếp theo
    }

    public void LoseGame()
    {
        Debug.Log("LoseGame action triggered - Loading Scene 0");
        SceneManager.LoadScene(0); // Hoặc scene menu chính/thử lại
    }
}