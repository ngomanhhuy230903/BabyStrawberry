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
    //MoveToTarget
    public void MoveToTarget(Vector2 targetPosition)
    {
        if (isMoving) return;
        isMoving = true;
        StartCoroutine(MoveToCoroutine(targetPosition));
    }
    //MoveToCoroutine
    private IEnumerator MoveToCoroutine(Vector2 targetPosition)
    {
        isMoving = true;
        float duration = 0.2f;
        Vector2 startPosition = transform.position;
        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            transform.position = Vector2.Lerp(startPosition, targetPosition, t);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;

        isMoving = false;
    }
}
