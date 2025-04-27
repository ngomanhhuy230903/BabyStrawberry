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
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit; // Sử dụng RaycastHit thay vì RaycastHit2D

            // Sử dụng Physics.Raycast thay vì Physics2D.Raycast
            if (Physics.Raycast(ray, out hit))
            {
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
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            InitializeBoard();
        }
        // Thêm vào Update()
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
        positionBoard = new Node[boardWidth, boardHeight];
        spaceingX = (float)((boardWidth - 1) / 2);
        spaceingY = (float)((boardHeight - 1) / 2) + 1;
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                // Thêm tọa độ Z = 0 rõ ràng
                Vector3 position = new Vector3((x - spaceingX) * spacingScale, (y - spaceingY) * spacingScale, 0);
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
        try
        {
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    //check if the current position is usable
                    if (positionBoard[x, y].isUsable && positionBoard[x, y].candy != null)
                    {
                        //then proceed to get candy class in node.
                        Candy candy = positionBoard[x, y].candy.GetComponent<Candy>();

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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in CheckBoard: {e.Message}\nStackTrace: {e.StackTrace}");
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
            if (positionBoard[x, y].isUsable && positionBoard[x, y].candy != null)
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
    private void SwapCandy(Candy selectedCandy, Candy targetCandy)
    {
        // In thông tin debug
        Debug.Log($"Attempting to swap: {selectedCandy.candyType}[{selectedCandy.xIndex},{selectedCandy.yIndex}] with {targetCandy.candyType}[{targetCandy.xIndex},{targetCandy.yIndex}]");

        // Kiểm tra kề nhau
        if (!IsAdjacent(selectedCandy, targetCandy))
        {
            Debug.LogWarning("Candies are not adjacent!");
            return;
        }

        // Đánh dấu đang xử lý để tránh swap nhiều lần
        isProcessingMove = true;

        // Gọi DoSwap để thực hiện swap
        DoSwap(selectedCandy, targetCandy);

        // Bắt đầu coroutine để xử lý kết quả của swap
        StartCoroutine(ProcessMatches(selectedCandy, targetCandy));
    }
    //do swap
    public void DoSwap(Candy selectedCandy, Candy targetCandy)
    {
        if (selectedCandy == null || targetCandy == null)
        {
            Debug.LogError("Cannot swap with null candy");
            return;
        }

        // Lưu chỉ số ban đầu
        int selectedX = selectedCandy.xIndex;
        int selectedY = selectedCandy.yIndex;
        int targetX = targetCandy.xIndex;
        int targetY = targetCandy.yIndex;

        Debug.Log($"DoSwap: Swapping [{selectedX},{selectedY}] with [{targetX},{targetY}]");

        // Kiểm tra chỉ số có hợp lệ không
        if (selectedX < 0 || selectedX >= boardWidth || selectedY < 0 || selectedY >= boardHeight ||
            targetX < 0 || targetX >= boardWidth || targetY < 0 || targetY >= boardHeight)
        {
            Debug.LogError("Invalid indices for candy swap");
            return;
        }

        // Lưu vị trí hiện tại của các viên kẹo
        Vector3 selectedPos = selectedCandy.transform.position;
        Vector3 targetPos = targetCandy.transform.position;

        // Cập nhật mảng positionBoard
        positionBoard[selectedX, selectedY].candy = targetCandy.gameObject;
        positionBoard[targetX, targetY].candy = selectedCandy.gameObject;

        // Cập nhật chỉ số của các viên kẹo
        selectedCandy.setIndicies(targetX, targetY);
        targetCandy.setIndicies(selectedX, selectedY);

        // Di chuyển các viên kẹo đến vị trí mới
        selectedCandy.MoveToTarget(targetPos);
        targetCandy.MoveToTarget(selectedPos);

        // Thêm visual feedback
        selectedCandy.SetSelected(false);
    }
    private IEnumerator ProcessMatches(Candy selectedCandy, Candy targetCandy)
    {
        Debug.Log("=== ProcessMatches START ===");

        if (selectedCandy == null || targetCandy == null)
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
            hasMatch = CheckBoard();
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
            DoSwap(selectedCandy, targetCandy);

            // Đợi animation swap back hoàn thành
            yield return new WaitForSeconds(0.3f);
        }

        // Reset
        selectedCandy = null;
        Debug.Log("=== ProcessMatches END ===");
        isProcessingMove = false;
    }
    //IsAdjacent
    private bool IsAdjacent(Candy selectedCandy, Candy targetCandy)
    {
        bool adjacent = Mathf.Abs(selectedCandy.xIndex - targetCandy.xIndex) +
                        Mathf.Abs(selectedCandy.yIndex - targetCandy.yIndex) == 1;

        Debug.Log($"IsAdjacent check: [{selectedCandy.xIndex},{selectedCandy.yIndex}] to [{targetCandy.xIndex},{targetCandy.yIndex}] = {adjacent}");

        return adjacent;
    }
    //ProcessMatched
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