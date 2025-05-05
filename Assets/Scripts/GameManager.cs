using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public GameObject backgroundPanel;
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    public int goal;// the number of points you need  to win
    public int moves;// the number of moves you have
    public int scores;// the number of points you have

    public bool isGameOver;
    private void Awake()
    {
            instance = this;
    }
    public void Initialize(int moves, int goal)
    {
        this.moves = moves;
        this.goal = goal;
        scores = 0;
        isGameOver = false;
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void ProcessTurn(int pointToGain,bool subtractMoves)
    {
        scores += pointToGain;
        if (subtractMoves)
        {
            moves--;
        }
        if (moves <= 0)
        {
            isGameOver = true;
            backgroundPanel.SetActive(true);
            victoryPanel.SetActive(true);
            return;
            LoseGame();
        }
        if (scores >= goal)
        {
            //you've win the game
            isGameOver = true;
            backgroundPanel.SetActive(true);
            victoryPanel.SetActive(true);
            return;
            WinGame();
        }
    }
    //attached to the button to change scene when winning
    public void WinGame()
    {
        SceneManager.LoadScene(0);
    }
    //attached to the button to change scene when losing
    public void LoseGame()
    {
        SceneManager.LoadScene(0);
    }
}
