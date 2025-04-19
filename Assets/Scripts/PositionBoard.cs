using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionBoard : MonoBehaviour
{
    //define the size of the board
    public int boardWidth = 6;
    public int boardHeight = 8;
    //define some spaceing for the board
    public float spaceingX;
    public float spaceingY;
    //get a reference to our position prefab
    public GameObject[] positionPrefab;
    //get a reference to the collection nodes positionBoard + GO
    public Node[,] positionBoard;
    public GameObject positionBoardGO;
    //layoutArray
    public ArrayLayout layoutArray;
    //public static of positionBoard
    public static PositionBoard instance;
    public void Awake()
    {
        instance = this;
    }
    public void Start()
    {
        InitializeBoard();
    }
    public void InitializeBoard()
    {
        positionBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)(boardWidth - 1) / 2;
        spaceingY = (float)((boardHeight - 1) / 2) + 1;
        for (int x = 0; x < boardHeight; x++)
        {
            for (int y = 0; y < boardWidth; y++)
            {
                Vector2 position = new Vector2((x - spaceingX), (y - spaceingY));
                int  randomIndex = Random.Range(0, positionPrefab.Length);
                GameObject candy = Instantiate(positionPrefab[randomIndex], position, Quaternion.identity);
                candy.GetComponent<Candy>().setIndicies(x, y);
                positionBoard[x, y] = new Node(true, candy);
            }
        }
    }

}
