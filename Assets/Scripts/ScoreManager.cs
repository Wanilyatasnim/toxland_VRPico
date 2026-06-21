using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static int scoreValue = 0;
    public TextMeshProUGUI score;

    // Update is called once per frame
    void Update()
    {
        score.text = "Score: " + scoreValue;
    }
}
