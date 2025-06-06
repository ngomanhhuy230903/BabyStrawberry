using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Chịu trách nhiệm xử lý toàn bộ logic của một lượt chơi sau khi có match.
/// Điều phối việc phá kẹo, hiệu ứng chuỗi, sụp đổ và lấp đầy bảng.
/// </summary>
public class BoardProcessor : MonoBehaviour
{
    // Tham chiếu đến các hệ thống khác
    private CandyBoard _board;
    private CandyFactory _candyFactory;
    private BoardMatcher _boardMatcher;

    private List<Candy> _candiesToProcess = new List<Candy>();
    private bool _isProcessingTurn = false;

    public void Initialize(CandyBoard board, CandyFactory factory, BoardMatcher matcher)
    {
        _board = board;
        _candyFactory = factory;
        _boardMatcher = matcher;
    }

    public void StartProcessingTurn(bool subtractMoves, List<Candy> initialMatches, Candy swapActivator = null, Candy swapTarget = null)
    {
        if (_isProcessingTurn) return;
        StartCoroutine(ProcessTurnCoroutine(subtractMoves, initialMatches, swapActivator, swapTarget));
    }

    private IEnumerator ProcessTurnCoroutine(bool subtractMoves, List<Candy> initialMatches, Candy swapActivator, Candy swapTarget)
    {
        _isProcessingTurn = true;
        _candiesToProcess.Clear();
        _candiesToProcess.AddRange(initialMatches);

        bool firstPass = true;

        if (swapActivator != null && swapActivator.specialEffect == SpecialCandyEffect.ClearColor)
        {
            HandleColorBombSwap(swapActivator, swapTarget);
        }

        while (_candiesToProcess.Count > 0)
        {
            HashSet<Candy> destroyedThisCascade = ResolveMatchesAndCreateSpecials(_candiesToProcess, firstPass ? initialMatches : null, swapActivator);

            if (destroyedThisCascade.Count > 0)
            {
                bool shouldSubtract = firstPass && subtractMoves;
                GameManager.instance?.ProcessTurn(destroyedThisCascade.Count, shouldSubtract);
            }

            firstPass = false;

            yield return new WaitForSeconds(0.4f);

            CollapseAndRefillAllColumns();

            yield return new WaitForSeconds(0.4f);

            _candiesToProcess.Clear();
            if (_boardMatcher.FindAllMatches(_board.candyBoard, _candiesToProcess))
            {
                Debug.Log("Cascade detected! Processing new matches.");
            }
        }

        _board.FinalizeCurrentTurnProcessing();
        _isProcessingTurn = false;
    }

    private HashSet<Candy> ResolveMatchesAndCreateSpecials(List<Candy> matchesToResolve, List<Candy> originalMatch, Candy swapActivator)
    {
        HashSet<Candy> allCandiesToDestroy = new HashSet<Candy>();
        Queue<Candy> processQueue = new Queue<Candy>(matchesToResolve);

        while (processQueue.Count > 0)
        {
            Candy currentCandy = processQueue.Dequeue();
            if (allCandiesToDestroy.Contains(currentCandy)) continue;

            allCandiesToDestroy.Add(currentCandy);

            if (currentCandy.isSpecial)
            {
                var newlyAffected = currentCandy.ExecuteSpecialEffectLogic(_board, null, allCandiesToDestroy);
                foreach (var newCandy in newlyAffected)
                {
                    if (!allCandiesToDestroy.Contains(newCandy))
                    {
                        processQueue.Enqueue(newCandy);
                    }
                }
            }
        }

        CreateSpecialCandyIfMatch(originalMatch, allCandiesToDestroy, swapActivator);

        HashSet<int> columnsToRefill = new HashSet<int>();
        foreach (var candy in allCandiesToDestroy)
        {
            if (candy == null || !candy.gameObject.activeSelf) continue;

            columnsToRefill.Add(candy.xIndex);

            if (_board.candyBoard[candy.xIndex, candy.yIndex].candy == candy.gameObject)
            {
                _board.candyBoard[candy.xIndex, candy.yIndex].candy = null;
            }
            _candyFactory.ReturnCandyToPool(candy);
        }
        _board.candyToRemove.RemoveAll(c => allCandiesToDestroy.Contains(c));

        return allCandiesToDestroy;
    }

    private void HandleColorBombSwap(Candy colorBomb, Candy otherCandy)
    {
        SpecialCandyEffect effect;
        if (otherCandy.specialEffect == SpecialCandyEffect.ClearColor) effect = SpecialCandyEffect.ClearBoard;
        else if (otherCandy.isSpecial) effect = SpecialCandyEffect.UpgradeColorToSpecials;
        else effect = SpecialCandyEffect.ClearColor;

        colorBomb.SetStrategyBasedOnEffect(effect);
        var affected = colorBomb.ExecuteSpecialEffectLogic(_board, otherCandy, new HashSet<Candy>());
        _candiesToProcess.AddRange(affected);
        _candiesToProcess.Add(colorBomb);
        _candiesToProcess.Add(otherCandy);
    }

    private void CreateSpecialCandyIfMatch(List<Candy> matchedCandies, HashSet<Candy> allCandiesBeingDestroyed, Candy swapActivator)
    {
        if (matchedCandies == null || matchedCandies.Count < 4) return;

        Candy primaryCandy = null;
        if (swapActivator != null && matchedCandies.Contains(swapActivator))
        {
            primaryCandy = swapActivator;
        }
        else
        {
            // Tìm một kẹo trong match không phải là kẹo đặc biệt (nếu có)
            primaryCandy = matchedCandies.FirstOrDefault(c => !c.isSpecial) ?? matchedCandies.First();
        }

        if (!allCandiesBeingDestroyed.Contains(primaryCandy)) return;

        int specialX = primaryCandy.xIndex;
        int specialY = primaryCandy.yIndex;
        CandyType originalType = primaryCandy.candyType;
        Vector3 specialPosition = new Vector3((specialX - _board.spaceingX) * _board.spacingScale, (specialY - _board.spaceingY) * _board.spacingScale, 0);

        bool isTOrLMatch = !matchedCandies.All(c => c.xIndex == specialX) && !matchedCandies.All(c => c.yIndex == specialY);

        Candy newSpecialCandy = null;
        if (matchedCandies.Count >= 5 && !isTOrLMatch)
        {
            newSpecialCandy = _candyFactory.CreateSpecialCandy(originalType, SpecialCandyEffect.ClearColor, specialX, specialY, specialPosition);
        }
        else if (matchedCandies.Count == 4 && !isTOrLMatch)
        {
            SpecialCandyEffect effect = (Random.Range(0, 2) == 0) ? SpecialCandyEffect.ClearRow : SpecialCandyEffect.ClearColumn;
            newSpecialCandy = _candyFactory.CreateSpecialCandy(originalType, effect, specialX, specialY, specialPosition);
        }

        if (newSpecialCandy != null)
        {
            _board.candyBoard[specialX, specialY].candy = newSpecialCandy.gameObject;
            allCandiesBeingDestroyed.Remove(primaryCandy);
        }
    }

    private void CollapseAndRefillAllColumns()
    {
        for (int x = 0; x < _board.boardWidth; x++)
        {
            CollapseColumn(x);
            FillEmptySpacesInColumn(x);
        }
    }

    private void CollapseColumn(int x)
    {
        for (int y = 0; y < _board.boardHeight - 1; y++)
        {
            if (_board.candyBoard[x, y].isUsable && _board.candyBoard[x, y].candy == null)
            {
                for (int aboveY = y + 1; aboveY < _board.boardHeight; aboveY++)
                {
                    if (_board.candyBoard[x, aboveY].isUsable && _board.candyBoard[x, aboveY].candy != null)
                    {
                        Candy candyToMove = _board.candyBoard[x, aboveY].candy.GetComponent<Candy>();
                        Vector3 targetPos = new Vector3((x - _board.spaceingX) * _board.spacingScale, (y - _board.spaceingY) * _board.spacingScale, 0);

                        candyToMove.MoveToTarget(targetPos);
                        candyToMove.setIndicies(x, y);

                        _board.candyBoard[x, y] = _board.candyBoard[x, aboveY];
                        _board.candyBoard[x, aboveY] = new Node(true, null);
                        break;
                    }
                }
            }
        }
    }

    private void FillEmptySpacesInColumn(int x)
    {
        for (int y = 0; y < _board.boardHeight; y++)
        {
            if (_board.candyBoard[x, y].isUsable && _board.candyBoard[x, y].candy == null)
            {
                List<int> availableTypes = GetAvailableCandyTypes(x, y);
                int randomIndex = availableTypes[Random.Range(0, availableTypes.Count)];
                CandyType typeToCreate = (CandyType)randomIndex;

                Vector3 spawnPos = new Vector3((x - _board.spaceingX) * _board.spacingScale, (_board.boardHeight - _board.spaceingY) * _board.spacingScale, 0);
                Vector3 targetPos = new Vector3((x - _board.spaceingX) * _board.spacingScale, (y - _board.spaceingY) * _board.spacingScale, 0);

                Candy newCandy = _candyFactory.CreateRegularCandy(typeToCreate, x, y, spawnPos);
                if (newCandy != null)
                {
                    newCandy.MoveToTarget(targetPos);
                    _board.candyBoard[x, y] = new Node(true, newCandy.gameObject);
                }
            }
        }
    }

    private List<int> GetAvailableCandyTypes(int x, int y)
    {
        List<int> availableTypes = new List<int>();
        for (int i = 0; i < _board.candyPrefabs.Length; i++) availableTypes.Add(i);

        if (x >= 2 && IsValidAndHasCandy(x - 1, y) && IsValidAndHasCandy(x - 2, y))
        {
            CandyType type1 = _board.candyBoard[x - 1, y].candy.GetComponent<Candy>().candyType;
            if (type1 == _board.candyBoard[x - 2, y].candy.GetComponent<Candy>().candyType)
                availableTypes.Remove((int)type1);
        }
        if (y >= 2 && IsValidAndHasCandy(x, y - 1) && IsValidAndHasCandy(x, y - 2))
        {
            CandyType type1 = _board.candyBoard[x, y - 1].candy.GetComponent<Candy>().candyType;
            if (type1 == _board.candyBoard[x, y - 2].candy.GetComponent<Candy>().candyType)
                availableTypes.Remove((int)type1);
        }
        if (availableTypes.Count == 0)
        {
            for (int i = 0; i < _board.candyPrefabs.Length; i++) availableTypes.Add(i);
        }
        return availableTypes;
    }

    private bool IsValidAndHasCandy(int x, int y)
    {
        return _board.candyBoard[x, y].isUsable && _board.candyBoard[x, y].candy != null;
    }
}