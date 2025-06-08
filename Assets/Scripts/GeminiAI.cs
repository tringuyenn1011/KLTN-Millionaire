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
    
    // Thuộc tính tĩnh để lưu kết quả tỷ lệ phần trăm từ GetAudiencePercentages
    private static int[] _audiencePercentages = new int[4];

    public int correctAnswerIndex; // 1-4 (A-D)
    public int idOfWrongAnswer;

    LifelinePhone lifelinePhone = new();
    LifelineAudience lifelineAudience = new();

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

        Debug.LogWarning(currentQuestionNumber);
        if (currentQuestionNumber == 1 || currentQuestionNumber == 6 || currentQuestionNumber == 11)
        {
            string level = GetDifficultyLevel(currentQuestionNumber);
            StartCoroutine(GenerateQuestionCoroutine(level));

        }
        else
        {
            // Vẫn còn câu hỏi trong pool, không cần gọi AI nữa
            UIManager.instance.ShowQuestion(
                questionPool[currentPoolIndex].question,
                questionPool[currentPoolIndex].answers
            );
        }

    }

    private string GetDifficultyLevel(int questionNumber)
    {
        if (questionNumber <= 5) return "cực dễ đến dễ";
        else if (questionNumber <= 10) return "dễ đến vừa";
        else return "vừa đến khó";
    }

    private IEnumerator GenerateQuestionCoroutine(string level)
    {
        int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            string prompt = $"Hãy tạo 5 câu hỏi trắc nghiệm kiến thức tổng quát đa dạng với 4 lựa chọn (A, B, C, D) cho mỗi câu, tất cả đều ở mức độ {level}, tránh các chủ đề đã sử dụng trước đó. Bao gồm đáp án đúng và một giải thích ngắn cho mỗi câu. Định dạng câu trả lời cho mỗi câu hỏi như sau:\n" +
                            "Question: <your question>\n" +
                            "A. <option1>\n" +
                            "B. <option2>\n" +
                            "C. <option3>\n" +
                            "D. <option4>\n" +
                            "Correct Answer: <A/B/C/D>\n" +
                            "Explanation: <short explanation>\n" +
                            "Lặp lại định dạng này cho cả 5 câu hỏi.";

            // string prompt = $"Generate 5 unique multiple-choice trivia questions about general knowledge with 4 options (A, B, C, D) each, all at the {level} difficulty level, avoiding topics already used (e.g., capitals, planets if previously asked). Include the correct answer and a short explanation for each. Format the response as follows for each question:\n" +
            //                 "Question: <your question>\n" +
            //                 "A. <option1>\n" +
            //                 "B. <option2>\n" +
            //                 "C. <option3>\n" +
            //                 "D. <option4>\n" +
            //                 "Correct Answer: <A/B/C/D>\n" +
            //                 "Explanation: <short explanation>\n" +
            //                 "Repeat this format for all 5 questions.";

            string safePrompt = prompt.Replace("\\", "\\\\").Replace('\"', '"');
            string jsonPayload = "{\"contents\":[{\"parts\":[{\"text\":\"" + safePrompt + "\"}]}]}";
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

            responseText = responseText.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n").Replace('\"', '"');
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
                UIManager.instance.ShowQuestion(questionPool[currentPoolIndex].question.Replace('\"', '"'), questionPool[currentPoolIndex].answers);
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
        Debug.LogWarning("run?");
        lifelinePhone = new LifelinePhone();

        string prompt = $"Hãy tạo một cuộc hội thoại gọi điện thoại thực tế cho quyền trợ giúp 'Gọi điện cho người thân' trong chương trình Ai Là Triệu Phú.\n\n" +
                        $"Người chơi (tên là Tôi) đang gọi cho một người bạn (tên gì cũng được) để hỏi câu hỏi sau:\n" +
                        $"Question: {questionPool[currentPoolIndex].question}\n" +
                        $"A. {questionPool[currentPoolIndex].answers[0]}\n" +
                        $"B. {questionPool[currentPoolIndex].answers[1]}\n" +
                        $"C. {questionPool[currentPoolIndex].answers[2]}\n" +
                        $"D. {questionPool[currentPoolIndex].answers[3]}\n\n" +
                        $"Đáp án đúng là {questionPool[currentPoolIndex].correctIndex}. Hãy mô phỏng một cuộc hội thoại gồm 5 đến 7 bước, trong đó người bạn hỗ trợ người chơi suy luận và dần đưa ra đáp án đúng một cách tự nhiên (Cảm ơn với nhau xong là được rồi).\n" +
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

    public void GenerateAudience()
    {
        StartCoroutine(GetAudiencePercentages(SetCorrectIndex(), GameProcess.instance.isLifeline5050JustUsed, 100));
    }
    // Phương thức mới để lấy tỷ lệ phần trăm từ API
    public IEnumerator GetAudiencePercentages(int idOfRightAnswer, bool is5050Used, int probabilityOfCorrectAnswer)
    {

        Debug.Log(idOfRightAnswer);
        for (int i = 0; i < 4; i++)
        {
            if (GameProcess.instance.isAnswerAvailable[i] == true && i + 1 != idOfRightAnswer)
            {
                idOfWrongAnswer = i + 1;
            }
        }
        _audiencePercentages = new int[4]; // Reset kết quả
        string difficulty = GetDifficultyLevel(GameProcess.instance.currentQuestionNumber);
        string prompt;
        if (is5050Used)
        {
            prompt = $"Simulate audience voting percentages for a 'Ask the Audience' lifeline in Who Wants to Be a Millionaire. Only two answers are available due to the 50:50 lifeline being used. The correct answer is {GetAnswerLetter(idOfRightAnswer)} (out of A, B, C, D). The difficulty level is {difficulty} (Easy, Medium, or Hard). For {difficulty} difficulty, the percentages should reflect the audience's likelihood of choosing the correct answer, with {probabilityOfCorrectAnswer}% chance of being correct. If difficulty is Easy, the correct answer should have a much higher percentage (e.g., 80 vs 20); if Medium, a moderate difference (e.g., 65 vs 35); if Hard, a smaller difference (e.g., 55 vs 45). The wrong answer is {GetAnswerLetter(idOfWrongAnswer)}. Provide the percentages in this format:\n" +
                     $"A: <percent>\n" +
                     $"B: <percent>\n" +
                     $"C: <percent>\n" +
                     $"D: <percent>\n" +
                     $"Correct Answer: {GetAnswerLetter(idOfRightAnswer)}\n" +
                     $"Ensure the sum of percentages for the two available answers is 100, and the other two answers are 0.";
        }
        else
        {
            prompt = $"Simulate audience voting percentages for a 'Ask the Audience' lifeline in Who Wants to Be a Millionaire. All four answers (A, B, C, D) are available. The correct answer is {GetAnswerLetter(idOfRightAnswer)}. The difficulty level is {difficulty} (Easy, Medium, or Hard). For {difficulty} difficulty, the percentages should reflect the audience's likelihood of choosing the correct answer, with {probabilityOfCorrectAnswer}% chance of being correct. If difficulty is Easy, the correct answer should have a much higher percentage (e.g., 70, 15, 10, 5); if Medium, a moderate difference (e.g., 50, 25, 15, 10); if Hard, a smaller difference (e.g., 30, 25, 25, 20). Provide the percentages in this format:\n" +
                     $"A: <percent>\n" +
                     $"B: <percent>\n" +
                     $"C: <percent>\n" +
                     $"D: <percent>\n" +
                     $"Correct Answer: {GetAnswerLetter(idOfRightAnswer)}\n" +
                     $"Ensure the sum of all percentages is 100.";
        }

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
            _audiencePercentages = new int[4]; // Reset và để LifelineAudience xử lý mặc định
        }
        else
        {
            string response = request.downloadHandler.text;
            Debug.Log("Phản hồi API: " + response);

            int textStart = response.IndexOf("\"text\": \"") + 9;
            if (textStart == 8)
            {
                Debug.LogError("Không thể tìm thấy text trong phản hồi: " + response);
                _audiencePercentages = new int[4];
                yield break;
            }

            int textEnd = textStart;
            bool inEscape = false;
            while (textEnd < response.Length)
            {
                char currentChar = response[textEnd];
                if (currentChar == '\\')
                {
                    inEscape = true;
                    textEnd++;
                    continue;
                }
                if (currentChar == '"' && !inEscape)
                {
                    break;
                }
                inEscape = false;
                textEnd++;
            }
            if (textEnd >= response.Length)
            {
                Debug.LogError("Phản hồi không đóng đúng: " + response);
                _audiencePercentages = new int[4];
                yield break;
            }

            string responseText = response.Substring(textStart, textEnd - textStart);
            Debug.Log("Text trích xuất: " + responseText);

            responseText = responseText.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = responseText.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
            
            if (lines.Length < 5)
            {
                Debug.LogError("Phản hồi không đủ dữ liệu: " + responseText);
                _audiencePercentages = new int[4];
                yield break;
            }

            for (int i = 0; i < 4; i++)
            {
                string line = lines[i+1];
                string percentStr = line.Split(':')[1].Trim();
                if (int.TryParse(percentStr, out int percent))
                {
                    _audiencePercentages[i] = percent;
                }
                else
                {
                    Debug.LogWarning($"Không thể phân tích tỷ lệ từ dòng: {line}, sử dụng giá trị mặc định.");
                    _audiencePercentages[i] = 0;
                }
            }

            // Kiểm tra tổng tỷ lệ
            int totalPercent = _audiencePercentages.Sum();
            if (totalPercent != 100)
            {
                Debug.LogWarning($"Tổng tỷ lệ không bằng 100% ({totalPercent}%), chuẩn hóa lại.");
                float scale = 100f / totalPercent;
                for (int i = 0; i < 4; i++)
                {
                    _audiencePercentages[i] = Mathf.RoundToInt(_audiencePercentages[i] * scale);
                }
                // Điều chỉnh lại để đảm bảo tổng là 100%
                int adjustedTotal = _audiencePercentages.Sum();
                if (adjustedTotal != 100)
                {
                    _audiencePercentages[0] += 100 - adjustedTotal;
                }
            }
        }
        lifelineAudience.Use();

    }

    // Phương thức để truy cập kết quả tỷ lệ phần trăm
    public int[] GetAudiencePercentagesResult()
    {
        return _audiencePercentages;
    }

    private string GetAnswerLetter(int answer)
    {
        return answer switch
        {
            1 => "A",
            2 => "B",
            3 => "C",
            4 => "D",
            _ => "A"
        };
    }
}