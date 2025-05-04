using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CandyBoard : MonoBehaviour
{
    //define the size of the board
    public int boardWidth = 6;
    public int boardHeight = 8;
    //define some spacing for the board
    public float spaceingX;
    public float spaceingY;
    public float spacingScale = 1.5f;
    //get a reference to our position prefab
    public GameObject[] candyPrefab;
    //get a reference to the collection nodes candyBoard + GO
    public Node[,] candyBoard;
    public GameObject candyBoardGO;

    public List<GameObject> candyToDestroy = new();
    public GameObject candyParent;
    [SerializeField] private Candy selectedCandy;
    [SerializeField] public bool isProcessingMove;
    //layoutArray
    public ArrayLayout arrayLayout;
    //public static of candyBoard
    public static CandyBoard instance;
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
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
            if (hit.collider != null)
            {
                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
                if (candy != null && !isProcessingMove)
                {
                    Debug.Log($"Click candy at position [{candy.xIndex},{candy.yIndex}], type: {candy.candyType}");
                    SelectCandy(candy);
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            InitializeBoard();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (selectedCandy != null)
                Debug.Log($"Current selectedCandy: {selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]");
            else
                Debug.Log("No candy selected");
        }
    }
    public void InitializeBoard()
    {
        DestroyPosition();
        candyBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2);
        spaceingY = (float)((boardHeight - 1) / 2) + 1;

        // Kiểm tra candyPrefab
        if (candyPrefab == null || candyPrefab.Length == 0)
        {
            Debug.LogError("candyBoard: candyPrefab array is null or empty. Please assign candy prefabs in the Inspector.");
            return;
        }

        for (int i = 0; i < candyPrefab.Length; i++)
        {
            if (candyPrefab[i] == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is null. Please assign a valid prefab in the Inspector.");
                return;
            }
            if (candyPrefab[i].GetComponent<Candy>() == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is missing Candy component.");
                return;
            }
            SpriteRenderer sr = candyPrefab[i].GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] is missing SpriteRenderer component.");
                return;
            }
            if (sr.sprite == null)
            {
                Debug.LogError($"candyBoard: candyPrefab[{i}] has SpriteRenderer but no sprite assigned.");
                return;
            }
        }

        // Kiểm tra arrayLayout
        if (arrayLayout == null || arrayLayout.rows == null || arrayLayout.rows.Length != boardHeight)
        {
            Debug.LogError("candyBoard: arrayLayout is null or has incorrect row count. Please configure ArrayLayout in the Inspector.");
            return;
        }
        for (int y = 0; y < boardHeight; y++)
        {
            if (arrayLayout.rows[y].row == null || arrayLayout.rows[y].row.Length != boardWidth)
            {
                Debug.LogError($"candyBoard: arrayLayout.rows[{y}].row is null or has incorrect length.");
                return;
            }
        }

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
                    int randomIndex = Random.Range(0, candyPrefab.Length);
                    Debug.Log($"Instantiating candy at [{x},{y}] with prefab: {candyPrefab[randomIndex].name}");
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
                        Debug.LogError($"Candy at [{x},{y}] is missing Candy component after instantiation.");
                        Destroy(candy);
                        continue;
                    }
                    candyComponent.setIndicies(x, y);
                    candyComponent.Init(x, y, (CandyType)randomIndex);
                    candyBoard[x, y] = new Node(true, candy);
                    candyToDestroy.Add(candy);
                }
            }
        }
        CheckBoard(true);
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
    public bool CheckBoard(bool takeAction)
    {
        Debug.Log("CheckBoard");
        bool hasMatch = false;
        List<Candy> candyToRemove = new List<Candy>();
        foreach(Node nodeCandy in candyBoard)
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
                    //check if the current position is usable
                    if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
                    {
                        //then proceed to get candy class in node.
                        Candy candy = candyBoard[x, y].candy.GetComponent<Candy>();

                        if (candy == null)
                        {
                            Debug.LogWarning($"Candy component not found at position [{x},{y}]");
                            continue;
                        }

                        //ensure its not matched
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
        if (takeAction)
        {
            foreach(Candy candy in candyToRemove)
            {
                candy.isMatched = false;
            }
            RemoveAndRefill(candyToRemove); //remove and refill
            if(CheckBoard(false)) //check the board again without taking action
            {
                CheckBoard(true);
            }
        }
        return hasMatch;
    }




    #region Cascading Candys
    //RemoveAndRefill
    private void RemoveAndRefill(List<Candy> candyToRemove)
    {
        //Removing the candy and clearing the board at the location
        foreach (Candy candy in candyToRemove)
        {
            //getting it's x and y indicites and storing them
            int xIndex = candy.xIndex;
            int yIndex = candy.yIndex;

            //Destroy the candy
            Destroy(candy.gameObject);
            //clear a blank node on the candy board
            candyBoard[xIndex, yIndex] = new Node(true, null);
        }
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (candyBoard[x,y].candy == null)
                {
                    Debug.Log("The location X: " + x + " Y: " + y + " is empty , attemping to refill it");
                    RefillCandy(x, y);
                }

            }
        }
        CheckBoard(false); //check the board again without taking action
        Debug.Log("Refill complete");
    }
    //RefillCandy
    private void RefillCandy(int x, int y)
    {
        // y offset
        int yOffset = 1;

        //while the cell above our current cell is null and we're not at the top of the board
        while (y + yOffset < boardHeight && candyBoard[x, y + yOffset].candy == null)
        {
            //increase y offset
            Debug.Log("The location X: " + x + " Y: " + y + " is empty, moving up to refill it");
            yOffset++;
        }
        //we've either hit the top of the board or we have a candy above us
        if (y + yOffset < boardHeight && candyBoard[x,y + yOffset].candy != null)
        {
            //we're found a candy above us
            Candy candyAbove = candyBoard[x, y + yOffset].candy.GetComponent<Candy>();

            //move it to the current position
            Vector3 targetPos = new Vector3(x - spaceingX, y - spaceingY, candyAbove.transform.position.z);
            Debug.Log("Moving candy from X: " + x + " Y: " + (y + yOffset) + " to X: " + x + " Y: " + y);
            //move the candy
            candyAbove.MoveToTarget(targetPos);
            //update indicites
            candyAbove.setIndicies(x, y);
            //update the candy board
            candyBoard[x, y] = candyBoard[x,y +yOffset];
            //set the candy above to null
            candyBoard[x, y + yOffset] = new Node(true, null);
        }
        //if we've hit the top of the board without finding a candy
        if (y + yOffset == boardHeight)
        {
            Debug.Log("I've reached the top of the board without finding a candy");
            SpawnCandyAtTop(x);
        }
    }

    //SpawnCandyAtTop

    private void SpawnCandyAtTop(int x)
    {
        int index = FindIndexOfLowestNull(x);
        int locationToMoveTo = 8 - index;
        Debug.Log("About to spawn a candy, ideally i'd like to put it in the index of: " + index);
        //get a random candy
        int randomIndex = Random.Range(0, candyPrefab.Length);
        GameObject newCandy = Instantiate(candyPrefab[randomIndex], new Vector2(x - spaceingX,boardHeight - spaceingY), Quaternion.identity);
        newCandy.transform.SetParent(candyParent.transform);
        //set the candy indicites
        newCandy.GetComponent<Candy>().setIndicies(x, index);
        //update the candy board
        candyBoard[x, locationToMoveTo] = new Node(true, newCandy);
        //move it to that location
        Vector3 targetPos = new Vector3(newCandy.transform.position.x, newCandy.transform.position.y - locationToMoveTo, newCandy.transform.position.z);
        newCandy.GetComponent<Candy>().MoveToTarget(targetPos);
    }


    //FindIndexOfLowestNull

    private int FindIndexOfLowestNull(int x)
    {
        int lowestNull = 99;
        for (int y = 7; y >= 0; y--)
        {
            if (candyBoard[x, y].candy == null)
            {
                lowestNull = y;
            }
        }
        return lowestNull;
    }
    #endregion

    private MatchResult SuperMatch(MatchResult matchCandy)
    {
        //if we have a horizontal or long horizontal match
        if(matchCandy.direction == MatchDirection.Horizontal || matchCandy.direction == MatchDirection.LongHorizontal)
        {        
            foreach (Candy candy in matchCandy.connectionCandys)// loop through the positions in my match
            {
                List<Candy> extraConnectionCandys = new List<Candy>();        //create new list of candys "extra matches"
                CheckDirection(candy, Vector2Int.up, extraConnectionCandys); //CheckDirection up
                CheckDirection(candy, Vector2Int.down, extraConnectionCandys);  //CheckDirection down
                if (extraConnectionCandys.Count >= 2)//do we have 2 or more extra match
                {
                    Debug.Log("I have a super horizontal match, the color is match is : " + matchCandy.connectionCandys[0].candyType);
                    extraConnectionCandys.AddRange(matchCandy.connectionCandys);
                    //we've made a super match - return a new matchresult of type super
                    return new MatchResult() { connectionCandys = extraConnectionCandys, direction = MatchDirection.Super };
                }
             }
            //return extra matches
            return new MatchResult() { connectionCandys = matchCandy.connectionCandys, direction = matchCandy.direction };

        }
        //if we have a vertical or long vertical match
        else if (matchCandy.direction == MatchDirection.Vertical || matchCandy.direction == MatchDirection.LongVertical)
        {
            foreach (Candy candy in matchCandy.connectionCandys)
            {
                List<Candy> extraConnectionCandys = new List<Candy>();
                CheckDirection(candy, Vector2Int.right, extraConnectionCandys); //check right
                CheckDirection(candy, Vector2Int.left, extraConnectionCandys); //check left
                if (extraConnectionCandys.Count >= 2)
                {
                    Debug.Log("I have a super vertical match, the color is match is : " + matchCandy.connectionCandys[0].candyType);
                    extraConnectionCandys.AddRange(matchCandy.connectionCandys);
                    return new MatchResult() { connectionCandys = extraConnectionCandys, direction = MatchDirection.Super };
                }
            }
            return new MatchResult() { connectionCandys = matchCandy.connectionCandys, direction = matchCandy.direction };
        }
        return null;
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
        if (connectionCandys.Count == 3)
        {
            Debug.Log("I have a normal horizontal match,the color is match is : " + connectionCandys[0].candyType);
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
        else if (connectionCandys.Count > 3)
        {
            Debug.Log("I have a long vertical match,the color is match is : " + connectionCandys[0].candyType);
            return new MatchResult() { connectionCandys = connectionCandys, direction = MatchDirection.LongVertical };
        }
        else
        {
            // Trả về một danh sách rỗng thay vì null
            return new MatchResult() { connectionCandys = new List<Candy>(), direction = MatchDirection.None };
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
            if (candyBoard[x, y].isUsable && candyBoard[x, y].candy != null)
            {
                //then proceed to get candy class in node.
                Candy nextCandy = candyBoard[x, y].candy.GetComponent<Candy>();
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
        if (candy == null)
        {
            Debug.LogError("Cannot select null candy");
            return;
        }

        Debug.Log($"SelectCandy called with candy type {candy.candyType} at [{candy.xIndex},{candy.yIndex}]");
        Debug.Log($"Current selectedCandy: {(selectedCandy == null ? "null" : selectedCandy.candyType.ToString() + " at [" + selectedCandy.xIndex + "," + selectedCandy.yIndex + "]")}");

        // Nếu đang xử lý, không cho chọn thêm
        if (isProcessingMove)
        {
            Debug.Log("Still processing previous move");
            return;
        }

        // Nếu chưa có kẹo nào được chọn trước đó
        if (selectedCandy == null)
        {
            selectedCandy = candy;
            selectedCandy.SetSelected(true); // Hiển thị visual feedback
            Debug.Log($"First candy selected: {selectedCandy.candyType} at [{selectedCandy.xIndex},{selectedCandy.yIndex}]");
        }
        // Nếu chọn lại chính kẹo đó
        else if (selectedCandy == candy)
        {
            selectedCandy.SetSelected(false); // Hủy visual feedback
            selectedCandy = null;
            Debug.Log("Deselected candy");
        }
        // Nếu chọn kẹo khác (tiến hành swap)
        else
        {
            Debug.Log($"Second candy selected: {candy.candyType} at [{candy.xIndex},{candy.yIndex}], attempting swap");

            // Lưu tham chiếu tạm thời để tránh vấn đề về tham chiếu
            Candy firstCandy = selectedCandy;
            Candy secondCandy = candy;

            // Xóa trạng thái chọn
            firstCandy.SetSelected(false);
            selectedCandy = null;

            // Thử swap
            SwapCandy(firstCandy, secondCandy);
        }
    }
    //swap candy-logic
    private void SwapCandy(Candy firstCandy, Candy secondCandy)
    {
        // In thông tin debug
        Debug.Log($"Attempting to swap: {firstCandy.candyType}[{firstCandy.xIndex},{firstCandy.yIndex}] with {secondCandy.candyType}[{secondCandy.xIndex},{secondCandy.yIndex}]");

        // Kiểm tra kề nhau
        if (!IsAdjacent(firstCandy, secondCandy))
        {
            Debug.LogWarning("Candies are not adjacent!");
            return;
        }

        // Đánh dấu đang xử lý để tránh swap nhiều lần
        isProcessingMove = true;

        // Gọi DoSwap để thực hiện swap
        DoSwap(firstCandy, secondCandy);

        // Bắt đầu coroutine để xử lý kết quả của swap
        StartCoroutine(ProcessMatches(firstCandy, secondCandy));
    }
    //do swap
    public void DoSwap(Candy firstCandy, Candy secondCandy)
    {
        if (firstCandy == null || secondCandy == null)
        {
            Debug.LogError("Cannot swap with null candy");
            isProcessingMove = false;
            return;
        }

        // Lưu chỉ số ban đầu
        int firstX = firstCandy.xIndex;
        int firstY = firstCandy.yIndex;
        int secondX = secondCandy.xIndex;
        int secondY = secondCandy.yIndex;

        Debug.Log($"DoSwap: Swapping [{firstX},{firstY}] with [{secondX},{secondY}]");

        // Kiểm tra chỉ số có hợp lệ không
        if (firstX < 0 || firstX >= boardWidth || firstY < 0 || firstY >= boardHeight ||
            secondX < 0 || secondX >= boardWidth || secondY < 0 || secondY >= boardHeight)
        {
            Debug.LogError("Invalid indices for candy swap");
            isProcessingMove = false;
            return;
        }

        // Lưu vị trí hiện tại của các viên kẹo
        Vector3 firstPos = firstCandy.transform.position;
        Vector3 secondPos = secondCandy.transform.position;

        // Cập nhật mảng candyBoard
        candyBoard[firstX, firstY].candy = secondCandy.gameObject;
        candyBoard[secondX, secondY].candy = firstCandy.gameObject;

        // Cập nhật chỉ số của các viên kẹo
        firstCandy.setIndicies(secondX, secondY);
        secondCandy.setIndicies(firstX, firstY);

        // Di chuyển các viên kẹo đến vị trí mới
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

        // Chờ cho animation di chuyển hoàn thành
        yield return new WaitForSeconds(0.3f);

        bool hasMatch = false;

        try
        {
            hasMatch = CheckBoard(true);
            Debug.Log($"CheckBoard result: {hasMatch}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in CheckBoard: {e.Message}\n{e.StackTrace}");
        }

        if (hasMatch)
        {
            // Xử lý kết quả match ở đây
            Debug.Log("Match found! Processing matches...");
            // Thêm code xử lý các viên kẹo đã match

            // Đợi một chút trước khi đánh dấu là đã hoàn thành
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            Debug.Log("No match found, swapping back");
            // Swap ngược lại
            DoSwap(firstCandy, secondCandy);

            // Đợi animation swap back hoàn thành
            yield return new WaitForSeconds(0.3f);
        }

        // Reset
        isProcessingMove = false;
        Debug.Log("=== ProcessMatches END ===");
    }
    //IsAdjacent
    private bool IsAdjacent(Candy firstCandy, Candy secondCandy)
    {
        bool adjacent = Mathf.Abs(firstCandy.xIndex - secondCandy.xIndex) +
                        Mathf.Abs(firstCandy.yIndex - secondCandy.yIndex) == 1;

        Debug.Log($"IsAdjacent check: [{firstCandy.xIndex},{firstCandy.yIndex}] to [{secondCandy.xIndex},{secondCandy.yIndex}] = {adjacent}");

        return adjacent;
    }
    #endregion
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