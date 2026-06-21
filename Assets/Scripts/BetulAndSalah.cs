using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BetulAndSalah : MonoBehaviour
{
    public GameObject btnName;// utk Betul or Salah Button
    public GameObject correctWrongName; // utk Right or Wrong Image
    public GameObject questionName;
    public GameObject explanationName;
    public GameObject btnDisappeared;

    public void clickBtn()
    {
        btnName.SetActive(false);
        btnDisappeared.SetActive(false);
        correctWrongName.SetActive(true);

        if (correctWrongName.name == "Right")
        {
            ScoreManager.scoreValue += 2;

        }
        else if (correctWrongName.name == "Wrong")
        {
            ScoreManager.scoreValue -= 2;
        }


        Invoke("DisplayExplanation", 3f); // tunggu 3 saat then baru keluar penerangan
    }

    void DisplayExplanation()
    {
        questionName.SetActive(false);
        btnName.SetActive(true);
        btnDisappeared.SetActive(true);
        correctWrongName.SetActive(false);
        explanationName.SetActive(true);
    }
}
