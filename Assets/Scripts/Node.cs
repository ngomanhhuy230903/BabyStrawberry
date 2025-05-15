using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    //to determine wether the space can be filled with position or not.
    public bool isUsable;

    public GameObject candy;

    public Node(bool isUsable, GameObject candy)
    {
        this.isUsable = isUsable;
        this.candy = candy;
    }
}
