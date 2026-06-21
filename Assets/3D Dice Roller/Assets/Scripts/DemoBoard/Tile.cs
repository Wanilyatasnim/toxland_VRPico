using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Each tile only knows what the next tile is.
/// </summary>

public class Tile : MonoBehaviour 
{
    public Tile lastTile;
    public Tile nextTile;
    public int extraMove;
    public GameObject videoCanvas;
    public bool questionToAnswer;
}