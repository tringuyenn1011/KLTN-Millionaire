using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;
using System;
using System.Collections.Generic;

public class GeminiAI : MonoBehaviour
{
    public static GeminiAI instance;

    [SerializeField] private string apiKey = "AIzaSyDa2W4tbVTFuHivVi9PcTmLpgFuFPtUowI"; // Có thể gán qua Inspector
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    public List<QuestionData> questionPool = new List<QuestionData>(); // Lưu 5 câu hiện tại
    private static List<(string text, float delay)> _phoneDialogResult = new List<(string, float)>();
    private int currentPoolIndex = 0; // Chỉ số trong pool hiện tại
    private string currentQuestion;
    private string[] currentAnswers;
    public int correctAnswerIndex; // 1-4 (A-D)

    LifelinePhone lifelinePhone = new();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    public int SetCorrectIndex()
    {
        return correctAnswerIndex = questionPool[currentPoolIndex].correctIndex;
    }
    public void GenerateQuestions(int currentQuestionNumber)
    {

        if (currentQuestionNumber <= 5)
        {
            currentPoolIndex = currentQuestionNumber - 1;
        }
        else if (currentQuestionNumber <= 10)
            currentPoolIndex = currentQuestionNumber - 5 - 1;
        else
            currentPoolIndex = currentQuestionNumber - 10 - 1;

        if (questionPool != null && currentPoolIndex < questionPool.Count)
        {
            // Vẫn còn câu hỏi trong pool, không cần gọi AI nữa
            UIManager.instance.ShowQuestion(
                questionPool[currentPoolIndex].question,
                questionPool[currentPoolIndex].answers
            );
        }
        else
        {
            string level = GetDifficultyLevel(currentQuestionNumber);
            StartCoroutine(GenerateQuestionCoroutine(level));
        }

    }

    private string GetDifficultyLevel(int questionNumber)
    {
        if (questionNumber <= 5) return "Easy";
        else if (questionNumber <= 10) return "Medium";
        else return "Hard";
    }

    private IEnumerator GenerateQuestionCoroutine(string level)
    {
        int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            string prompt = $"Generate 5 unique multiple-choice trivia questions about general knowledge with 4 options (A, B, C, D) each, all at the {level} difficulty level, avoiding topics already used (e.g., capitals, planets if previously asked). Include the correct answer and a short explanation for each. Format the response as follows for each question:\n" +
                            "Question: <your question>\n" +
                            "A. <option1>\n" +
                            "B. <option2>\n" +
                            "C. <option3>\n" +
                            "D. <option4>\n" +
                            "Correct Answer: <A/B/C/D>\n" +
                            "Explanation: <short explanation>\n" +
                            "Repeat this format for all 5 questions.";

            string jsonPayload = "{\"contents\":[{\"parts\":[{\"text\":\"" + prompt + "\"}]}]}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

            UnityWebRequest request = new UnityWebRequest(apiUrl + "?key=" + apiKey, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;

            float startTime = Time.time;
            yield return request.SendWebRequest();
            float endTime = Time.time;
            Debug.Log($"Thời gian xử lý (lần {attempt + 1}, cấp độ {level}): {endTime - startTime} giây");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Lỗi API: " + request.error + "\nChi tiết: " + request.downloadHandler.text);
                if (attempt == maxRetries - 1) SetDefaultQuestion();
                continue;
            }

            string response = request.downloadHandler.text;
            Debug.Log("Phản hồi đầy đủ: " + response);

            int startIndex = response.IndexOf("\"text\": \"") + 9;
            if (startIndex == 8)
            {
                Debug.LogError("Không thể tìm thấy text trong phản hồi: " + response);
                if (attempt == maxRetries - 1) SetDefaultQuestion();
                continue;
            }

            int endIndex = startIndex;
            bool inEscape = false;
            while (endIndex < response.Length)
            {
                char currentChar = response[endIndex];
                if (currentChar == '\\')
                {
                    inEscape = true;
                    endIndex++;
                    continue;
                }
                if (currentChar == '"' && !inEscape)
                {
                    break;
                }
                inEscape = false;
                endIndex++;
            }
            if (endIndex >= response.Length)
            {
                Debug.LogError("Phản hồi không đóng đúng: " + response);
                if (attempt == maxRetries - 1) SetDefaultQuestion();
                continue;
            }

            string responseText = response.Substring(startIndex, endIndex - startIndex);

            responseText = responseText.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = responseText.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();

            Debug.Log("Số dòng sau khi tách: " + lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                Debug.Log($"Line {i}: {lines[i]}");
            }

            if (lines.Length < 7 * 5)
            {
                Debug.LogError("Phản hồi không đủ 5 câu: " + responseText);
                if (attempt == maxRetries - 1) SetDefaultQuestion();
                continue;
            }

            questionPool.Clear();
            for (int i = 0; i < lines.Length; i += 7)
            {
                if (i + 6 >= lines.Length) break;
                string tempQuestion = lines[i].Replace("Question: ", "");
                if (!tempQuestion.Contains("?"))
                {
                    Debug.LogWarning("Câu hỏi không đầy đủ: " + tempQuestion);
                    continue;
                }
                string[] tempAnswers = new string[4]
                {
                    lines[i + 1].Replace("A. ", ""),
                    lines[i + 2].Replace("B. ", ""),
                    lines[i + 3].Replace("C. ", ""),
                    lines[i + 4].Replace("D. ", "")
                };
                string correctAnswerLine = lines[i + 5].Replace("Correct Answer: ", "").Trim();
                int tempCorrectIndex = correctAnswerLine switch
                {
                    "A" => 1,
                    "B" => 2,
                    "C" => 3,
                    "D" => 4,
                    _ => 1
                };
                questionPool.Add(new QuestionData(tempQuestion, tempAnswers, tempCorrectIndex));
            }


            if (questionPool.Count == 0)
            {
                Debug.LogError("Không thể tạo pool câu hỏi: " + responseText);
                if (attempt == maxRetries - 1) SetDefaultQuestion();
                continue;
            }

            currentPoolIndex = 0;
            if (UIManager.instance != null)
            {
                UIManager.instance.ShowQuestion(questionPool[currentPoolIndex].question, questionPool[currentPoolIndex].answers);
                break;
            }
            else
            {
                Debug.LogError("UIManager.instance is null.");
                SetDefaultQuestion();
                break;
            }
        }
    }

    private void SetDefaultQuestion()
    {
        string defaultQuestion = "What is the capital of Vietnam?";
        string[] defaultAnswers = new string[] { "A. Ho Chi Minh City", "B. Hanoi", "C. Da Nang", "D. Can Tho" };
        if (UIManager.instance != null)
        {
            UIManager.instance.ShowQuestion(defaultQuestion, defaultAnswers);
        }
    }

    public class QuestionData
    {
        public string question;
        public string[] answers;
        public int correctIndex;

        public QuestionData(string q, string[] a, int c)
        {
            question = q;
            answers = a;
            correctIndex = c;
        }
    }

    public void GeneratePhoneDialog()
    {
        StartCoroutine(GetPhoneDialog(correctAnswerIndex));
    }
    public IEnumerator GetPhoneDialog(int answer)
    {
        _phoneDialogResult.Clear(); // Xóa kết quả cũ


        string prompt = $"Generate a realistic phone call conversation for the 'Phone a Friend' lifeline in Who Wants to Be a Millionaire.\n\n" +
                        $"The contestant is calling a friend to ask the following question:\n" +
                        $"Question: {questionPool[currentPoolIndex].question}\n" +
                        $"A. {questionPool[currentPoolIndex].answers[0]}\n" +
                        $"B. {questionPool[currentPoolIndex].answers[1]}\n" +
                        $"C. {questionPool[currentPoolIndex].answers[2]}\n" +
                        $"D. {questionPool[currentPoolIndex].answers[3]}\n\n" +
                        $"The correct answer is {questionPool[currentPoolIndex].correctIndex}. Simulate a 5-7 step conversation where the friend helps reach that correct answer naturally.\n" +
                        $"Each step must follow this format:\n" +
                        $"Step: <description>\n" +
                        $"Text: <dialog text>\n" +
                        $"Delay: <time in seconds>\n";

        string jsonPayload = "{\"contents\":[{\"parts\":[{\"text\":\"" + prompt + "\"}]}]}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        UnityWebRequest request = new UnityWebRequest(apiUrl + "?key=" + apiKey, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 20;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Lỗi API: " + request.error + "\nChi tiết: " + request.downloadHandler.text);
            _phoneDialogResult = new List<(string, float)>
            {
                ("\t  Calling...\n", 3f),
                ("\t  Calling...\n\t- Yes", 1.5f),
                ("\t- Yes\n\t- Hello, it's Who wants to be a millionaire!", 2f),
                ("\t- Hello, it's Who wants to be a millionaire!\n\t  How do you think, which answer is right?", 1.5f),
                ("\t  How do you think, which answer is right?\n\t- Hmmmm...", 2f),
                ("\t- Hmmmm...\n\t- I think it's " + answer, 1.5f)
            };
        }
        else
        {
            string response = request.downloadHandler.text;
            Debug.Log("Phản hồi API: " + response);

            int startIndex = response.IndexOf("\"text\": \"") + 9;
            if (startIndex == 8)
            {
                Debug.LogError("Không thể tìm thấy text trong phản hồi: " + response);
                _phoneDialogResult = new List<(string, float)>
                {
                    ("\t  Calling...\n", 3f),
                    ("\t  Calling...\n\t- Yes", 1.5f),
                    ("\t- Yes\n\t- Hello, it's Who wants to be a millionaire!", 2f),
                    ("\t- Hello, it's Who wants to be a millionaire!\n\t  How do you think, which answer is right?", 1.5f),
                    ("\t  How do you think, which answer is right?\n\t- Hmmmm...", 2f),
                    ("\t- Hmmmm...\n\t- I think it's " + answer, 1.5f)
                };
            }
            else
            {
                int endIndex = startIndex;
                bool inEscape = false;
                while (endIndex < response.Length)
                {
                    char currentChar = response[endIndex];
                    if (currentChar == '\\')
                    {
                        inEscape = true;
                        endIndex++;
                        continue;
                    }
                    if (currentChar == '"' && !inEscape)
                    {
                        break;
                    }
                    inEscape = false;
                    endIndex++;
                }
                string responseText = response.Substring(startIndex, endIndex - startIndex);

                responseText = responseText.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
                string[] lines = responseText.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToArray();

                Debug.Log("Số dòng sau khi tách: " + lines.Length);
                for (int i = 0; i < lines.Length; i++)
                {
                    Debug.Log($"Line {i}: {lines[i]}");
                }

                for (int i = 0; i < lines.Length; i += 3)
                {
                    if (i + 2 >= lines.Length) break;
                    string text = lines[i + 1].Replace("Text: ", "");
                    if (float.TryParse(lines[i + 2].Replace("Delay: ", ""), out float delay))
                    {
                        _phoneDialogResult.Add((text, delay));
                    }
                }

                if (_phoneDialogResult.Count < 5)
                {
                    Debug.LogError("Phản hồi không đủ 5 bước: " + responseText);
                    _phoneDialogResult = new List<(string, float)>
                    {
                        ("\t  Calling...\n", 3f),
                        ("\t  Calling...\n\t- Yes", 1.5f),
                        ("\t- Yes\n\t- Hello, it's Who wants to be a millionaire!", 2f),
                        ("\t- Hello, it's Who wants to be a millionaire!\n\t  How do you think, which answer is right?", 1.5f),
                        ("\t  How do you think, which answer is right?\n\t- Hmmmm...", 2f),
                        ("\t- Hmmmm...\n\t- I think it's " + answer, 1.5f)
                    };
                }
            }
        }

        lifelinePhone.Use();

    }

    // Thêm phương thức để truy cập kết quả
    public static List<(string text, float delay)> GetPhoneDialogResult()
    {
        return _phoneDialogResult;
    }
}