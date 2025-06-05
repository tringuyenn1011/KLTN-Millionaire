﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using System.Collections.Generic;

public class LifelinePhone : MonoBehaviour
{
    Animator timerAnimator;

    int persentsOfRightAnswer = 99;

    [SerializeField] private GeminiAI aiGenerator; // Tham chiếu đến GeminiAI
    private List<(string text, float delay)> dialogLines = new List<(string, float)>();


    void Start()
    {
        if (aiGenerator == null)
        {
            aiGenerator = GeminiAI.instance;
            if (aiGenerator == null)
            {
                Debug.LogError("Không tìm thấy GeminiAI trong scene!");
                return;
            }
        }
    }
    public void Use()
    {
        aiGenerator = GeminiAI.instance;

        timerAnimator = UIManager.instance.phonePanel.transform.GetChild(1).GetComponent<Animator>();

        if (GameProcess.instance.isLifeline5050JustUsed)
        {
            //probability of true equals to persentsOfRightAnswer%
            if (Random.Range(1, 101) < persentsOfRightAnswer)
            {
                //returnig correct answer
                ApplyLifeline(GameProcess.instance.question.CorrectAnswer);
            }
            else
            {
                int idOfWrongAnswer = 1; // (1 to 4)                

                //finding id of wrong answer
                for (int i = 0; i < 4; i++)
                {
                    if (GameProcess.instance.isAnswerAvailable[i] == true && GameProcess.instance.question.CorrectAnswer != i + 1)
                    {
                        idOfWrongAnswer = i + 1;
                    }

                }
                ApplyLifeline(idOfWrongAnswer);
            }
        }
        else
        {

            //probability of true equals to persentsOfRightAnswer%
            if (Random.Range(1, 101) < persentsOfRightAnswer)
            {
                //returnig correct answer
                ApplyLifeline(GameProcess.instance.question.CorrectAnswer);
            }
            else
            {
                //returning wrong answer
                int wrongAnswer;

                do
                {
                    wrongAnswer = Random.Range(1, 5);
                }
                while (wrongAnswer == GameProcess.instance.question.CorrectAnswer);
                ApplyLifeline(wrongAnswer);
            }
        }


    }

    /// <summary>
    /// Applying lifeline
    /// </summary>
    /// <param name="answer">friend's answer, number of question(1 to 4)</param>
    void ApplyLifeline(int answer)
    {

        switch (answer)
        {
            case 1:
                UIManager.instance.StartCoroutine(LifelinePhoneAnimation("A"));
                break;
            case 2:
                UIManager.instance.StartCoroutine(LifelinePhoneAnimation("B"));
                break;
            case 3:
                UIManager.instance.StartCoroutine(LifelinePhoneAnimation("C"));
                break;
            case 4:
                UIManager.instance.StartCoroutine(LifelinePhoneAnimation("D"));
                break;
        }


        //making lifelinePhone button not interactable
        UIManager.instance.moneyTreePanel.transform.GetChild(2).GetComponent<Image>().sprite = UIManager.instance.moneyTreeSprites[8];
        UIManager.instance.moneyTreePanel.transform.GetChild(2).GetComponent<Button>().interactable = false;
    }

    public IEnumerator newc()
    {
        Debug.Log("new co");
        yield return null;
    }

    public IEnumerator LifelinePhoneAnimation(string answer)
    {
        GameProcess.instance.PauseMusic();
        GameProcess.instance.PlaySoundByNumber(68);
        UIManager.instance.StartCoroutine(UIManager.instance.CloseMoneyTreePanel());
        UIManager.instance.phonePanel.SetActive(true);

        Debug.Log("Calling...");
        UIManager.instance.phoneDialogText.text = "\t  Calling...\n";

        yield return new WaitForSeconds(3f);

        //Debug.Log("- Yes");
        UIManager.instance.phoneDialogText.text = "\t  Calling...\n\t- Yes";

        yield return new WaitForSeconds(1.5f);

        //Debug.Log("- Hello, it's Who wants to be a milionire! How do you thing, which answer is right?f");
        UIManager.instance.phoneDialogText.text = "\t- Yes\n\t- Hello, it's Who wants to be a milionire!";
        yield return new WaitForSeconds(2f);
        UIManager.instance.phoneDialogText.text = "\t- Hello, it's Who wants to be a milionire!\n\t  How do you thing, which answer is right?";

        yield return new WaitForSeconds(1.5f);

        int timeOfAnswer = 30;/*Random.Range(0, 25);*/ // time on tiner when friend will give an answer
        int timer = 30;

        UIManager.instance.phonePanel.transform.GetChild(1).gameObject.SetActive(true);
            //litle dellay
        yield return new WaitForSeconds(0.255f);
        GameProcess.instance.PlaySoundByNumber(69);

        while (timer >= 0)
        {
            UIManager.instance.phonePanel.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = "" + timer;


            // if (timeOfAnswer == timer)
            // {
                if (timeOfAnswer >= 1)
                {
                    GameProcess.instance.PlaySoundByNumber(83);
                    
                }

                Debug.LogWarning(dialogLines.Count);
                // Hiển thị từng dòng hội thoại
                foreach (var dialog in dialogLines)
                {
                    Debug.LogWarning(dialog);
                    Debug.LogWarning("oke");
                    UIManager.instance.phoneDialogText.text = dialog.text;
                    yield return new WaitForSeconds(dialog.delay + 3);
                }

                UIManager.instance.phonePanel.transform.GetChild(1).GetComponent<Animator>().SetBool("HideTimer", true);
                UIManager.instance.phonePanel.transform.GetChild(0).gameObject.SetActive(false);

                UIManager.instance.phonePanel.transform.GetChild(3).GetComponent<Button>().interactable = true;
                GameProcess.instance.UnPauseMusic();
            // }

            timer--;
            yield return new WaitForSeconds(1f);
        }



        Debug.LogWarning($"aiGenerator: {aiGenerator}");
        if (aiGenerator != null)
        {
            dialogLines.Clear();
            // Lấy kết quả từ thuộc tính tĩnh
            dialogLines = GeminiAI.GetPhoneDialogResult();
        }
        else
        {
            Debug.LogError("aiGenerator is null! Sử dụng hội thoại mặc định.");
            dialogLines = new List<(string, float)>
            {
                ("\t  Calling...\n", 3f),
                ("\t  Calling...\n\t- Yes", 1.5f),
                ("\t- Yes\n\t- Hello, it's Who wants to be a millionaire!", 2f),
                ("\t- Hello, it's Who wants to be a millionaire!\n\t  How do you think, which answer is right?", 1.5f),
                ("\t  How do you think, which answer is right?\n\t- Hmmmm...", 2f),
                ("\t- Hmmmm...\n\t- I think it's " + answer, 1.5f)
            };
        }

        

        // Kết thúc animation
        UIManager.instance.phonePanel.transform.GetChild(1).GetComponent<Animator>().SetBool("HideTimer", true);
        UIManager.instance.phonePanel.transform.GetChild(0).gameObject.SetActive(false);
        UIManager.instance.phonePanel.transform.GetChild(3).GetComponent<Button>().interactable = true;
        GameProcess.instance.UnPauseMusic();
    }

    // public IEnumerator LifelinePhoneAnimation(string answer)
    // {
    //     GameProcess.instance.PauseMusic();
    //     GameProcess.instance.PlaySoundByNumber(68);
    //     UIManager.instance.StartCoroutine(UIManager.instance.CloseMoneyTreePanel());
    //     UIManager.instance.phonePanel.SetActive(true);

    //     //Debug.Log("Calling...");
    //     UIManager.instance.phoneDialogText.text = "\t  Calling...\n";

    //     yield return new WaitForSeconds(3f);

    //     //Debug.Log("- Yes");
    //     UIManager.instance.phoneDialogText.text = "\t  Calling...\n\t- Yes";

    //     yield return new WaitForSeconds(1.5f);

    //     //Debug.Log("- Hello, it's Who wants to be a milionire! How do you thing, which answer is right?f");
    //     UIManager.instance.phoneDialogText.text = "\t- Yes\n\t- Hello, it's Who wants to be a milionire!";
    //     yield return new WaitForSeconds(2f);
    //     UIManager.instance.phoneDialogText.text = "\t- Hello, it's Who wants to be a milionire!\n\t  How do you thing, which answer is right?";

    //     yield return new WaitForSeconds(1.5f);

    //     int timeOfAnswer = Random.Range(0, 25); // time on tiner when friend will give an answer
    //     int timer = 30;

    //     UIManager.instance.phonePanel.transform.GetChild(1).gameObject.SetActive(true);



    //     //Debug.Log("- Hmmmm...");
    //     UIManager.instance.phoneDialogText.text = "\t  How do you thing, which answer is right?\n\t- Hmmmm...";
    //     timerAnimator.SetBool("StartCountdown", true);


    //     //litle dellay
    //     yield return new WaitForSeconds(0.255f);
    //     GameProcess.instance.PlaySoundByNumber(69);

    //     while (timer >= 0)
    //     {
    //         UIManager.instance.phonePanel.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = "" + timer;


    //         if (timeOfAnswer == timer)
    //         {
    //             if (timeOfAnswer >= 1)
    //             {
    //                 GameProcess.instance.PlaySoundByNumber(83);
    //             }
    //             UIManager.instance.phoneDialogText.text = "\t- Hmmmm...\n\t- I think it's " + answer;
    //             UIManager.instance.phonePanel.transform.GetChild(1).GetComponent<Animator>().SetBool("HideTimer", true);
    //             UIManager.instance.phonePanel.transform.GetChild(0).gameObject.SetActive(false);

    //             UIManager.instance.phonePanel.transform.GetChild(3).GetComponent<Button>().interactable = true;
    //             GameProcess.instance.UnPauseMusic();
    //         }

    //         timer--;
    //         yield return new WaitForSeconds(1f);
    //     }
    // }
}
