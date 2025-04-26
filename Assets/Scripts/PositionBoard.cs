using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

    public List<GameObject> positionToDestroy = new();
    [SerializeField] private Candy selectedCandy;
    [SerializeField] private bool isProcessingMove;
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
    public void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
            if (hit.collider != null && hit.collider.gameObject.GetComponent<Candy>())
            {
                if (isProcessingMove)
                {
                    return;
                }
                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
                Debug.Log("I have click a candy it is " + candy.gameObject);
                SelectCandy(candy);
            }
        }   
        if (Input.GetKeyDown(KeyCode.Space))
        {
            InitializeBoard();
        }
    }
    public void InitializeBoard()
    {
        DestroyPosition();
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
                    positionToDestroy.Add(candy);
                }

            }
        }
        CheckBoard();
    }
    private void DestroyPosition()
    {
        if (positionToDestroy != null)
        {
            foreach (GameObject position in positionToDestroy)
            {
                Destroy(position);
            }
            positionToDestroy.Clear();
        }
    }
        public bool CheckBoard()
    {
        Debug.Log("CheckBoard");
        bool hasMatch = false;
        List<Candy> candyToRemove = new List<Candy>();
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                //check if the current position is usable
                if (positionBoard[x, y].isUsable)
                {
                    //then proceed to get candy class in node.
                    Candy candy = positionBoard[x, y].candy.GetComponent<Candy>();

                    //ensure its not matched
                    if (!candy.isMatched)
                    {
                        MatchResult matchCandy = IsConnected(candy);
                        if(matchCandy.connectionCandys.Count >= 3)
                        {
                            //comlext matching
                            candyToRemove.AddRange(matchCandy.connectionCandys);
                            foreach (Candy c in matchCandy.connectionCandys)
                            {
                                c.isMatched = true;
                            }
                            hasMatch = true;
                        }
                    }
                }
            }
        }
        return hasMatch;
    }
    //IsConnected
    MatchResult IsConnected(Candy candy)
    {
        List<Candy> connectionCandys = new List<Candy>();
        CandyType candyType = candy.candyType;
        connectionCandys.Add(candy);
        //check right
        CheckDirection(candy, Vector2Int.right, connectionCandys); //check right
        //check left
        CheckDirection(candy, Vector2Int.left, connectionCandys); //check left
        //check we have a 3 match?(Horizontal match)
        if(connectionCandys.Count == 3)
        {
            Debug.Log("I have a normal horizontal match,the color is match is : "+ connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.Horizontal };
        }
        //checking for more than 3 match(long horizontal match)
        else if (connectionCandys.Count > 3)
        {
            Debug.Log("I have a long horizontal match,the color is match is : " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.LongHorizontal };
        }
        //clear out the connectionCandys list
        connectionCandys.Clear();
        //read our initial candy
        connectionCandys.Add(candy);
        //check up
        CheckDirection(candy, Vector2Int.up, connectionCandys); //check up
        //check down
        CheckDirection(candy, Vector2Int.down, connectionCandys); //check down
                                                                  //check we have a 3 match?(Vertical match)
        if (connectionCandys.Count == 3)
        {
            Debug.Log("I have a normal Vertical match,the color is match is : " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.Vertical };
        }
        //checking for more than 3 match(long vertical match)
        else if(connectionCandys.Count > 3)
        {
            Debug.Log("I have a long vertical match,the color is match is : " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.LongVertical };
        }
        else { 
            return new MatchResult() { connectionCandys = null, direction = MatchDirection.None };
        }
    }

    void CheckDirection(Candy candy, Vector2Int direction, List<Candy> connectionCandys)
    {
        CandyType candyType = candy.candyType;
        int x = candy.xIndex + direction.x;
        int y = candy.yIndex + direction.y;
        //check if we are within the bounds of the board
        while (x >= 0 && x < boardWidth && y >= 0 && y < boardHeight)
        {
            //check if the current position is usable
            if (positionBoard[x, y].isUsable)
            {
                //then proceed to get candy class in node.
                Candy nextCandy = positionBoard[x, y].candy.GetComponent<Candy>();
                //does our candyType match? it must also not be matched
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
    #region Swapping candys
    
    //select candy
    public void SelectCandy(Candy candy)
    {
        //if we don't have a candy currently selected,then set the candy i just clicked to my selected candy
        if(selectedCandy == null)
        {
            selectedCandy = candy;
            Debug.Log("Selected candy is: " + selectedCandy.candyType);
        }
        //if we select the same candy twice, then let's make selected candy null
        else if(selectedCandy == candy)
        {
            selectedCandy = null;
            Debug.Log("Selected candy is: " + selectedCandy);
        }
        //if selected candy is not null and is not  the current candy, then we can swap the two candys

        //selected candy back to null
        else if (selectedCandy != candy)
        {
            SwapCandy(selectedCandy, candy);
            selectedCandy = null;
        }
    }
    //swap candy-logic
    private void SwapCandy(Candy selectedCandy, Candy targetCandy)
    {
        //!IsAdjacent don't do anything
        if(!IsAdjacent(selectedCandy, targetCandy))
        {
            Debug.Log("Selected candy is not adjacent to the candy selected");
            return;
        }
        //Do swap
        DoSwap(selectedCandy, targetCandy);
        isProcessingMove = true;
        //startCoroutine ProcessMatches.
        StartCoroutine(ProcessMatches(selectedCandy, targetCandy));
    }
    //do swap
    public void DoSwap(Candy selectedCandy, Candy targetCandy)
    {
        GameObject temp = positionBoard[selectedCandy.xIndex, selectedCandy.yIndex].candy;
        positionBoard[selectedCandy.xIndex, selectedCandy.yIndex].candy = positionBoard[targetCandy.xIndex, targetCandy.yIndex].candy;
        positionBoard[targetCandy.xIndex, targetCandy.yIndex].candy = temp;
        //update the indicies of the candy
        int tempXindex = selectedCandy.xIndex;
        int tempYindex = selectedCandy.yIndex;
        selectedCandy.xIndex = targetCandy.xIndex;
        selectedCandy.yIndex = targetCandy.yIndex;
        targetCandy.xIndex = tempXindex;
        targetCandy.yIndex = tempYindex;
        selectedCandy.MoveToTarget(positionBoard[selectedCandy.xIndex, selectedCandy.yIndex].candy.transform.position);
        targetCandy.MoveToTarget(positionBoard[targetCandy.xIndex, targetCandy.yIndex].candy.transform.position);
    }
    private IEnumerator ProcessMatches(Candy selectedCandy, Candy targetCandy)
    {
        yield return new WaitForSeconds(0.2f);
        bool hasMatch = CheckBoard();
        if(hasMatch)
        {
            //do something
            Debug.Log("I have a match");
        }
        else
        {
            //swap back
            DoSwap(targetCandy, selectedCandy);
            Debug.Log("I don't have a match");
        }
        isProcessingMove = false;
    }
    //IsAdjacent
    private bool IsAdjacent(Candy selectedCandy, Candy targetCandy)
    {
        return Mathf.Abs(selectedCandy.xIndex - targetCandy.xIndex) + Mathf.Abs(selectedCandy.yIndex - targetCandy.yIndex) == 1;
    }
    //ProcessMatched
    #endregion
}


public class MatchResult
{
    public List<Candy> connectionCandys;
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