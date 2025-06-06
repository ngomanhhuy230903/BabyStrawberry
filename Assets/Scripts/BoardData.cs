using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class Node
{
    public bool isUsable;
    public GameObject candy;

    public Node(bool isUsable, GameObject candy)
    {
        this.isUsable = isUsable;
        this.candy = candy;
    }
}

[System.Serializable]
public class ArrayLayout
{
    [System.Serializable]
    public struct rowData
    {
        public bool[] row;
    }

    public rowData[] rows = new rowData[8];
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