using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CandyType
{

    Red,
    Yellow,
    Green,
    Purple,
    Blue,
    Orange
} 
public class Candy : MonoBehaviour
{
    [SerializeField] public int xIndex;
    [SerializeField] public int yIndex;
    [SerializeField] public int currentPosition;
    [SerializeField] public int targetPosition;
    [SerializeField] public bool isMoving;
    [SerializeField] public bool isMatched;
    [SerializeField] public CandyType candyType;
    // Start is called before the first frame update
    public Candy(int xIndex, int yIndex)
    {
        this.xIndex = xIndex;
        this.yIndex = yIndex;
    }
    public void setIndicies(int xIndex, int yIndex)
    {
        this.xIndex = xIndex;
        this.yIndex = yIndex;
    }
}
