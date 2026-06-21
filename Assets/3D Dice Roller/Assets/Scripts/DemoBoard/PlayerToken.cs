using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerToken : MonoBehaviour
{
    [Tooltip("The tile the PlayerToken will start on.")]
    public Tile startingTile;
    public DiceManager diceManager;
    public TextMeshProUGUI valueText;
    public GameObject RollDiceUI;
    public GameObject btnRollDice;
    public GameObject btnRestart;

    public List<GameObject> QandA;
    public List<GameObject> redVideo;
    public List<GameObject> blueVideo;

    public List<Tile> tileRotate180;
    public List<Tile> tileRotate90;
    public List<Tile> tileRotate0;
    public List<Tile> tileRotateNegative90;
    //mini habits day 8
    Tile[] moveQueue;
    int moveQueueIndex;

    Vector3 targetPosition;
    Vector3 velocity;
    Vector3 startPosition;

    Tile finalTile;
    int randomNumber;

    void Awake()
    {
        finalTile = startingTile;
        targetPosition = this.transform.position;
        startPosition = this.transform.position;
    }

    // Update is called once per frame
    /// <summary>
    /// Moves this object to the desired position which is set by the dice roll.
    /// </summary>
    void Update()
    {

        if (Vector3.Distance(this.transform.position, targetPosition) > 0.03f)//move player to the next
        {
            this.transform.position = Vector3.SmoothDamp(this.transform.position, targetPosition, ref velocity, 0.2f);
        }

        else// after move player to the next, set next point as new target position
        {
            if (moveQueue != null && moveQueueIndex < moveQueue.Length)
            {
                Tile nextTile = moveQueue[moveQueueIndex];
                SetNewTargetPosition(nextTile.transform.position);
                moveQueueIndex++;
            }

            //for randomly shows the blue video
            else if (moveQueue != null && moveQueueIndex == moveQueue.Length && finalTile.extraMove > 0)
            {
                RollDiceUI.SetActive(false);
                randomNumber = Random.Range(0, 7);
                blueVideo[randomNumber].SetActive(true);
                moveQueue = null;
            }
            //for randomly shows the red video
            else if (moveQueue != null && moveQueueIndex == moveQueue.Length && finalTile.extraMove < 0)
            {
                RollDiceUI.SetActive(false);
                randomNumber = Random.Range(0, 7);
                redVideo[randomNumber].SetActive(true);
                moveQueue = null;
            }
            //for randomly shows the Q&A
            else if (moveQueue != null && moveQueueIndex == moveQueue.Length && finalTile.questionToAnswer)
            {
                RollDiceUI.SetActive(false);
                randomNumber = Random.Range(0, 20);
                QandA[randomNumber].SetActive(true);
                moveQueue = null;
            }

            /*
            else if (moveQueue != null && moveQueueIndex == moveQueue.Length && (finalTile.extraMove != 0 || finalTile.questionToAnswer))
            {
                RollDiceUI.SetActive(false);
                if (finalTile.videoCanvas != null)
                    finalTile.videoCanvas.SetActive(true);
                moveQueue = null;
            }*/


        }
        //rotate the direction of player token into the next tile

        if (this.transform.position.z < tileRotateNegative90[0].transform.position.z - 0.25)
        {
            if (this.transform.position.x < tileRotate180[0].transform.position.x - 0.25)
                this.transform.eulerAngles = new Vector3(0, 180, 0);
            else if (this.transform.position.x > tileRotate0[0].transform.position.x + 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, 90, 0);
        }

        else if (this.transform.position.z > tileRotate90[0].transform.position.z && this.transform.position.z < tileRotate90[1].transform.position.z - 0.25)
        {
            if (this.transform.position.x < tileRotate0[1].transform.position.x - 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, -90, 0);
        }

        else if (this.transform.position.z > tileRotateNegative90[0].transform.position.z && this.transform.position.z < tileRotateNegative90[1].transform.position.z - 0.25)
        {
            if (this.transform.position.x > tileRotate0[2].transform.position.x + 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, 90, 0);
        }

        else if (this.transform.position.z > tileRotate90[1].transform.position.z && this.transform.position.z < tileRotate90[2].transform.position.z - 0.25)
        {
            if (this.transform.position.x < tileRotate0[3].transform.position.x - 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, -90, 0);
        }

        else if (this.transform.position.z > tileRotateNegative90[1].transform.position.z && this.transform.position.z < tileRotateNegative90[2].transform.position.z - 0.25)
        {
            if (this.transform.position.x > tileRotate0[4].transform.position.x + 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, 90, 0);
        }

        else if (this.transform.position.z > tileRotate90[2].transform.position.z && this.transform.position.z < tileRotate90[3].transform.position.z - 0.25)
        {
            if (this.transform.position.x < tileRotate0[5].transform.position.x - 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, -90, 0);
        }

        else if (this.transform.position.z > tileRotateNegative90[2].transform.position.z && this.transform.position.z < tileRotateNegative90[3].transform.position.z - 0.25)
        {
            if (this.transform.position.x > tileRotate0[6].transform.position.x + 0.25)
                this.transform.eulerAngles = new Vector3(0, 0, 0);
            else
                this.transform.eulerAngles = new Vector3(0, 90, 0);
        }

        else if (this.transform.position.z > tileRotate90[3].transform.position.z)
        {
            if (this.transform.position.x < tileRotate180[1].transform.position.x - 0.25)
                this.transform.eulerAngles = new Vector3(0, 180, 0);
            else
                this.transform.eulerAngles = new Vector3(0, -90, 0);
        }
    }


    void SetNewTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
        velocity = Vector3.zero;
    }

    /// <summary>
    /// Moves the player 1-6 spaces depending on value of the dice roll.
    /// </summary>
    public void MovePlayerToken()//create a array of moving steps
    {
        int spacesToMove = diceManager.totalValue;
        //int spacesToMove = 55;

        valueText.text = "Value: " + spacesToMove.ToString();

        if (spacesToMove == 0)
        {
            return;
        }

        moveQueue = new Tile[spacesToMove];

        for (int i = 0; i < spacesToMove; i++)
        {

            finalTile = finalTile.nextTile;
            moveQueue[i] = finalTile;

            if (finalTile == finalTile.nextTile)
            {
                ScoreManager.scoreValue += 10;
                valueText.text = "Congrats! Arrive the final point!";
                btnRollDice.SetActive(false);
                btnRestart.SetActive(true);
                break;
                //move backward
                /*if (i == spacesToMove - 1)
                {
                    ScoreManager.scoreValue += 10;
                    valueText.text = "Congrats! Arrive the final point!";
                    btnRollDice.SetActive(false);
                    btnRestart.SetActive(true);
                }
                else
                {
                    for (int j = i + 1; j < spacesToMove; j++)
                    {
                        finalTile = finalTile.lastTile;
                        moveQueue[j] = finalTile;
                    }
                }
                break;
                */
            }
        }
        moveQueueIndex = 0;
    }

    public void ExtraMovePlayerToken()
    {

        if (finalTile.extraMove > 0)
        {
            valueText.text = "Good Behavior!! Move = " + finalTile.extraMove;
            int spaceToMove = finalTile.extraMove;
            moveQueue = new Tile[spaceToMove];

            for (int i = 0; i < spaceToMove; i++)
            {
                finalTile = finalTile.nextTile;
                moveQueue[i] = finalTile;
            }
        }

        else if (finalTile.extraMove < 0)
        {
            valueText.text = "Bad Behavior!! Move = " + finalTile.extraMove;
            int spacesToMove = finalTile.extraMove * -1;
            moveQueue = new Tile[spacesToMove];

            for (int i = 0; i < spacesToMove; i++)
            {
                finalTile = finalTile.lastTile;
                moveQueue[i] = finalTile;
            }
        }
        moveQueueIndex = 0;
    }
    public void resetPosition()
    {
        moveQueue = null;
        finalTile = startingTile;
        SetNewTargetPosition(startPosition);
        ScoreManager.scoreValue = 0;
        valueText.text = "Value: 0";
        btnRestart.SetActive(false);
        btnRollDice.SetActive(true);
    }
}