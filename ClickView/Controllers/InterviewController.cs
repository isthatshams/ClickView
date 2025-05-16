using Microsoft.AspNetCore.Mvc;
using ClickView.Models;
using ClickView.DTOs;
using ClickView.Data;
using Microsoft.AspNetCore.Authorization;

namespace ClickView.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]

    public class InterviewController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public InterviewController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartInterview(StartInterviewDto dto)
        {
            if (!_db.Users.Any(u => u.UserId == dto.UserId))
                return NotFound("User not found.");

            if (!Enum.TryParse<InterviewType>(dto.InterviewType, true, out var parsedType))
                return BadRequest("Invalid interview type. Must be 'Chat' or 'Voice'.");

            if (string.IsNullOrWhiteSpace(dto.Level))
                return BadRequest("Interview level is required.");

            var allowedLevels = new[] { "internship", "junior", "mid", "senior" };
            var normalizedLevel = dto.Level.ToLower();
            if (!allowedLevels.Contains(normalizedLevel))
                return BadRequest("Invalid level. Must be: internship, junior, mid, or senior.");

            if (string.IsNullOrWhiteSpace(dto.JobTitle))
                return BadRequest("Job title is required before starting the interview.");

            List<string> questionsFromGPT;
            int? usedCvId = null;

            if (dto.CvId.HasValue)
            {
                var cv = await _db.CVs.FindAsync(dto.CvId.Value);
                if (cv == null || cv.UserId != dto.UserId)
                    return BadRequest("Invalid or unauthorized CV.");

                if (!string.IsNullOrWhiteSpace(cv.ExtractedText))
                {
                    questionsFromGPT = await GetQuestionsFromChatGPTUsingCv(cv.ExtractedText, normalizedLevel);
                    usedCvId = cv.CvId;
                }
                else
                {
                    return BadRequest("Selected CV has no readable text.");
                }
            }
            else
            {
                // No CV: use job title as GPT topic
                questionsFromGPT = await GetQuestionsFromChatGPT(normalizedLevel, dto.JobTitle);
            }

            var generatedQuestions = questionsFromGPT.Select(q => new Question
            {
                QuestionText = q,
                DifficultyLevel = Enum.TryParse<DifficultyLevel>(normalizedLevel, true, out var levelEnum) ? levelEnum : DifficultyLevel.Junior,
                QuestionMark = 5
            }).ToList();

            var interview = new Interview
            {
                UserId = dto.UserId,
                InterviewType = parsedType,
                InterviewMark = 0,
                CvId = usedCvId,
                Questions = generatedQuestions
            };

            _db.Interviews.Add(interview);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Interview started", interviewId = interview.InterviewId });
        }

        private List<Question> GenerateMockQuestions(InterviewType type)
        {
            var difficulty = type == InterviewType.Chat ? DifficultyLevel.Internship : DifficultyLevel.Mid;

            return new List<Question>
            {
                new Question { QuestionText = "Tell me about yourself.", DifficultyLevel = difficulty, QuestionMark = 5 },
                new Question { QuestionText = "What are your strengths?", DifficultyLevel = difficulty, QuestionMark = 5 },
                new Question { QuestionText = "Where do you see yourself in 5 years?", DifficultyLevel = difficulty, QuestionMark = 5 }
            };
        }

        [HttpPost("answer")]
        public IActionResult SubmitAnswer(AnswerQuestionDto dto)
        {
            var interview = _db.Interviews.Find(dto.InterviewId);
            var question = _db.Questions.Find(dto.QuestionId);

            if (interview == null || question == null)
                return NotFound("Interview or Question not found.");

            var answer = new UserAnswer
            {
                InterviewId = dto.InterviewId,
                QuestionId = dto.QuestionId,
                UserAnswerText = dto.UserAnswerText,
                UserAnswerNotes = dto.Notes
            };

            _db.UserAnswers.Add(answer);
            _db.SaveChanges();

            return Ok(new { message = "Answer submitted", answerId = answer.UserAnswerId });
        }

        [HttpGet("{id}")]
        public IActionResult GetInterview(int id)
        {
            var interview = _db.Interviews
                .Where(i => i.InterviewId == id)
                .Select(i => new
                {
                    i.InterviewId,
                    i.InterviewType,
                    i.InterviewMark,
                    i.UserId,
                    Questions = i.Questions.Select(q => new
                    {
                        q.QuestionId,
                        q.QuestionText,
                        q.DifficultyLevel,
                        q.QuestionMark
                    }),
                    Answers = i.UserAnswers.Select(a => new
                    {
                        a.UserAnswerId,
                        a.QuestionId,
                        a.UserAnswerText,
                        a.UserAnswerNotes
                    })
                })
                .FirstOrDefault();

            if (interview == null)
                return NotFound("Interview not found.");

            return Ok(interview);
        }
        [HttpGet("user/{userId}")]
        public IActionResult GetUserInterviews(int userId)
        {
            var user = _db.Users.Find(userId);
            if (user == null) return NotFound("User not found.");

            var interviews = _db.Interviews
                .Where(i => i.UserId == userId)
                .Select(i => new
                {
                    i.InterviewId,
                    i.InterviewType,
                    i.InterviewMark,
                    StartedAt = i.StartedAt,
                    QuestionCount = i.Questions.Count,
                    AnswerCount = i.UserAnswers.Count
                })
                .ToList();

            return Ok(interviews);
        }
        [HttpGet("{interviewId}/summary")]
        public IActionResult GetInterviewSummary(int interviewId)
        {
            var interview = _db.Interviews
                .Where(i => i.InterviewId == interviewId)
                .Select(i => new
                {
                    i.InterviewId,
                    i.InterviewType,
                    i.InterviewMark,
                    TotalQuestions = i.Questions.Count,
                    TotalAnswers = i.UserAnswers.Count
                })
                .FirstOrDefault();

            if (interview == null)
                return NotFound("Interview not found.");

            return Ok(interview);
        }
        private async Task<List<string>> GetQuestionsFromChatGPT(string level, string topic)
        {
            using var client = new HttpClient();
            var payload = new
            {
                level = level,
                topic = topic
            };

            var response = await client.PostAsJsonAsync("https://cac6-34-150-163-215.ngrok-free.app/generate-questions", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            return result["questions"];
        }
        private async Task<List<string>> GetQuestionsFromChatGPTUsingCv(string extractedText, string level)
        {
            using var client = new HttpClient();

            var payload = new
            {
                cv_text = extractedText,
                level = level
            };

            var response = await client.PostAsJsonAsync("https://your-ngrok-url.ngrok.io/generate-questions", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            return result["questions"];
        }

    }
}
