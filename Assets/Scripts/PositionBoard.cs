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
    public float spacingScale = 1.5f;
    //get a reference to our position prefab
    public GameObject[] positionPrefab;
    //get a reference to the collection nodes positionBoard + GO
    public Node[,] positionBoard;
    public GameObject positionBoardGO;
    //layoutArray
    public ArrayLayout arrayLayout;
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
        spaceingX = (float)((boardWidth - 1) / 2);
        spaceingY = (float)((boardHeight - 1) / 2) + 1;
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                Vector2 position = new Vector2((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale);
                if (arrayLayout.rows[y].row[x])
                {
                    positionBoard[x, y] = new Node(false, null);
                }
                else
                {
                    int randomIndex = Random.Range(0, positionPrefab.Length);
                    GameObject candy = Instantiate(positionPrefab[randomIndex], position, Quaternion.identity);
                    candy.GetComponent<Candy>().setIndicies(x, y);
                    positionBoard[x, y] = new Node(true, candy);
                }
                
            }
        }
    }
    public bool CheckBoard()
    {
        Debug.Log("CheckBoard");
        bool hasMatch = false;
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                //check if the current position is usable
                if(positionBoard[x, y].isUsable)
                {
                    //then proceed to get candy class in node.
                    Candy candy = positionBoard[x, y].candy.GetComponent<Candy>();

                    //ensure its not matched
                    if (!candy.isMatched)
                    {
                        MatchResult matchCandy = IsConnected(candy);
                    }
                }
            }
        }
        return false;
    }
    //IsConnected
    MatchResult IsConnected(Candy candy)
    {
        List<Candy> connectionCandys = new List<Candy>();
        CandyType candyType = candy.candyType;
        connectionCandys.Add(candy);
        //check right

        //check left

        //check we have a 3 match?(Horizontal match)

        //checking for more than 3 match(long horizontal match)

        //clear out the connectionCandys list

        //read our initial candy

        //check up

        //check down

        //check we have a 3 match?(Vertical match)

        //checking for more than 3 match(long vertical match)
    }

    //CheckDirection
}


public class MatchResult
{
    List<Candy> connectionCandys;
    MatchDirection direction;
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