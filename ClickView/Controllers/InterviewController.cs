using Microsoft.AspNetCore.Mvc;
using ClickView.Models;
using ClickView.DTOs;
using ClickView.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http.Json;

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
                var cv = await _db.CVs
                    .Include(c => c.Insights)
                    .FirstOrDefaultAsync(c => c.CvId == dto.CvId.Value && c.UserId == dto.UserId);

                if (cv == null)
                    return BadRequest("Invalid or unauthorized CV.");

                if (cv.Insights != null)
                {
                    questionsFromGPT = await GetQuestionsFromInsights(cv.Insights, normalizedLevel,dto.JobTitle );
                    usedCvId = cv.CvId;
                }
                else if (!string.IsNullOrWhiteSpace(cv.ExtractedText))
                {
                    string combinedText = $"Job Title: {dto.JobTitle}\n\nCV:\n{cv.ExtractedText}";
                    questionsFromGPT = await GetQuestionsFromChatGPTUsingCv(cv.ExtractedText, dto.JobTitle, normalizedLevel);
                    usedCvId = cv.CvId;
                }
                else
                {
                    return BadRequest("Selected CV has no insights or readable text.");
                }
            }
            else
            {
                //No CV: use job title as GPT topic
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

            interview.OriginalInterviewId = dto.OriginalInterviewId ?? interview.InterviewId;

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

            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-questions", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            return result["questions"];
        }
        private async Task<List<string>> GetQuestionsFromChatGPTUsingCv(string extractedText, string jobTitle, string level)
        {
            using var client = new HttpClient();

            var payload = new
            {
                level,
                cv_text = $"Job Title: {jobTitle}\n\nCV:\n{extractedText}"
            };

            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-questions", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            return result?["questions"] ?? new List<string>();
        }
        private async Task<List<string>> GetQuestionsFromInsights(CvInsights insights, string jobTitle, string level)
        {
            using var client = new HttpClient();
            var payload = new
            {
                level,
                cv_text = $"""
        Job Title: {jobTitle}
        Technical Skills: {insights.TechnicalSkills}
        Tools and Technologies: {insights.ToolsAndTechnologies}
        Soft Skills: {insights.SoftSkills}
        Certifications: {insights.Certifications}
        Experience Summary: {insights.ExperienceSummary}
        """
            };

            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-questions", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            return result?["questions"] ?? new List<string>();
        }

        [Authorize]
        [HttpPost("{interviewId}/retake-same")]
        public async Task<IActionResult> RetakeSameInterview(int interviewId)
        {
            var original = await _db.Interviews
                .Include(i => i.Questions)
                .FirstOrDefaultAsync(i => i.InterviewId == interviewId);

            if (original == null)
                return NotFound("Original interview not found.");

            var userId = int.Parse(User.FindFirstValue("userId")!);
            if (original.UserId != userId)
                return Forbid();

            var clonedQuestions = original.Questions.Select(q => new Question
            {
                QuestionText = q.QuestionText,
                DifficultyLevel = q.DifficultyLevel,
                QuestionMark = q.QuestionMark
            }).ToList();

            var newInterview = new Interview
            {
                UserId = userId,
                InterviewType = original.InterviewType,
                InterviewMark = 0,
                CvId = original.CvId,
                Questions = clonedQuestions,
                OriginalInterviewId = original.OriginalInterviewId ?? original.InterviewId
            };

            _db.Interviews.Add(newInterview);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Retake started", interviewId = newInterview.InterviewId });
        }

        [HttpPost("next-question")]
        public async Task<IActionResult> GetAdaptiveFollowup([FromBody] AdaptiveFollowupDto dto)
        {
            var interview = _db.Interviews
                .Include(i => i.Questions)
                .Include(i => i.UserAnswers)
                .Include(i => i.CV)
                .FirstOrDefault(i => i.InterviewId == dto.InterviewId);
            if (interview == null)
                return NotFound("Interview not found.");

            var lastQuestion = interview.Questions.FirstOrDefault(q => q.QuestionId == dto.LastQuestionId);
            if (lastQuestion == null)
                return NotFound("Last question not found.");

            // Gather previous Q&A
            var previousQA = interview.UserAnswers
                .OrderBy(a => a.UserAnswerId)
                .Where(a => a.QuestionId != dto.LastQuestionId)
                .Select(a => new { question = interview.Questions.FirstOrDefault(q => q.QuestionId == a.QuestionId)?.QuestionText ?? "", answer = a.UserAnswerText })
                .ToList();

            // Compose request for Python API
            var requestBody = new
            {
                job_title = interview.CV?.JobTitle ?? "",
                level = interview.Questions.FirstOrDefault()?.DifficultyLevel.ToString().ToLower() ?? "junior",
                cv_text = interview.CV?.ExtractedText ?? "",
                previous_qa = previousQA,
                last_question = lastQuestion.QuestionText,
                last_answer = dto.LastAnswerText
            };

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-followup", requestBody);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Failed to get follow-up question from AI service.");
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var followupText = result?["question"] ?? "";
            if (string.IsNullOrWhiteSpace(followupText))
                return StatusCode(500, "AI did not return a valid question.");

            // Store the new question
            var followupQuestion = new Question
            {
                QuestionText = followupText,
                DifficultyLevel = lastQuestion.DifficultyLevel,
                QuestionMark = 5,
                InterviewId = interview.InterviewId
            };
            _db.Questions.Add(followupQuestion);
            await _db.SaveChangesAsync();

            return Ok(new { question = followupText, questionId = followupQuestion.QuestionId });
        }

        // VOICE INTERVIEW ENDPOINTS
        [HttpPost("voice/start")]
        public async Task<IActionResult> StartVoiceInterview([FromBody] StartVoiceInterviewDto dto)
        {
            // Validate user
            var user = await _db.Users.FindAsync(dto.UserId);
            if (user == null) return NotFound("User not found.");

            // Get CV if provided
            CV cv = null;
            if (dto.CvId.HasValue)
                cv = await _db.CVs.FindAsync(dto.CvId.Value);

            // Create interview
            var interview = new Interview
            {
                UserId = dto.UserId,
                InterviewType = InterviewType.Voice,
                CvId = dto.CvId,
                Questions = new List<Question>(),
                UserAnswers = new List<UserAnswer>()
            };
            _db.Interviews.Add(interview);
            await _db.SaveChangesAsync();

            // Generate first question using Python API
            var payload = new
            {
                level = dto.Level.ToLower(),
                cv_text = cv?.ExtractedText ?? "",
                topic = dto.JobTitle
            };
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-questions", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            var firstQuestionText = result?["questions"]?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstQuestionText))
                return StatusCode(500, "Failed to generate first question.");
            var firstQuestion = new Question
            {
                QuestionText = firstQuestionText,
                DifficultyLevel = Enum.TryParse<DifficultyLevel>(dto.Level, true, out var lvl) ? lvl : DifficultyLevel.Junior,
                QuestionMark = 5,
                InterviewId = interview.InterviewId
            };
            _db.Questions.Add(firstQuestion);
            await _db.SaveChangesAsync();
            return Ok(new { interviewId = interview.InterviewId, question = firstQuestion.QuestionText, questionId = firstQuestion.QuestionId });
        }

        [HttpPost("voice/answer-next")]
        public async Task<IActionResult> SubmitVoiceAnswerAndGetNext([FromBody] VoiceAnswerDto dto)
        {
            // Find interview and last question
            var interview = _db.Interviews.Include(i => i.Questions).Include(i => i.UserAnswers).FirstOrDefault(i => i.InterviewId == dto.InterviewId && i.InterviewType == InterviewType.Voice);
            if (interview == null) return NotFound("Interview not found.");
            var lastQuestion = interview.Questions.FirstOrDefault(q => q.QuestionId == dto.QuestionId);
            if (lastQuestion == null) return NotFound("Question not found.");

            // Save answer
            var answer = new UserAnswer
            {
                InterviewId = dto.InterviewId,
                QuestionId = dto.QuestionId,
                UserAnswerText = dto.UserAnswerText
            };
            _db.UserAnswers.Add(answer);
            await _db.SaveChangesAsync();

            // Prepare Q&A context for next question
            var previousQA = interview.UserAnswers.Select(a => new { question = interview.Questions.FirstOrDefault(q => q.QuestionId == a.QuestionId)?.QuestionText ?? "", answer = a.UserAnswerText }).ToList();
            var payload = new
            {
                job_title = interview.CV?.JobTitle ?? "",
                level = lastQuestion.DifficultyLevel.ToString().ToLower(),
                cv_text = interview.CV?.ExtractedText ?? "",
                previous_qa = previousQA,
                last_question = lastQuestion.QuestionText,
                last_answer = dto.UserAnswerText
            };
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-followup", payload);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Failed to get follow-up question from AI service.");
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var followupText = result?["question"] ?? "";
            if (string.IsNullOrWhiteSpace(followupText))
                return Ok(new { message = "Interview complete" }); // No more questions
            var followupQuestion = new Question
            {
                QuestionText = followupText,
                DifficultyLevel = lastQuestion.DifficultyLevel,
                QuestionMark = 5,
                InterviewId = interview.InterviewId
            };
            _db.Questions.Add(followupQuestion);
            await _db.SaveChangesAsync();
            return Ok(new { question = followupText, questionId = followupQuestion.QuestionId });
        }

        // CHAT INTERVIEW ENDPOINTS
        [HttpPost("chat/start")]
        public async Task<IActionResult> StartChatInterview([FromBody] StartTextInterviewDto dto)
        {
            // Validate user
            var user = await _db.Users.FindAsync(dto.UserId);
            if (user == null) return NotFound("User not found.");
            CV cv = null;
            if (dto.CvId.HasValue)
                cv = await _db.CVs.FindAsync(dto.CvId.Value);
            // Create interview
            var interview = new Interview
            {
                UserId = dto.UserId,
                InterviewType = InterviewType.Chat,
                CvId = dto.CvId,
                Questions = new List<Question>(),
                UserAnswers = new List<UserAnswer>()
            };
            _db.Interviews.Add(interview);
            await _db.SaveChangesAsync();
            // Generate initial batch of questions (3–5, diverse topics)
            var payload = new
            {
                level = dto.Level.ToLower(),
                cv_text = cv?.ExtractedText ?? "",
                topic = dto.JobTitle
            };
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-questions", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
            var questions = result?["questions"]?.Take(5).ToList() ?? new List<string>();
            var initialQuestions = questions.Select(q => new Question
            {
                QuestionText = q,
                DifficultyLevel = Enum.TryParse<DifficultyLevel>(dto.Level, true, out var lvl) ? lvl : DifficultyLevel.Junior,
                QuestionMark = 5,
                InterviewId = interview.InterviewId,
                ParentQuestionId = null
            }).ToList();
            _db.Questions.AddRange(initialQuestions);
            await _db.SaveChangesAsync();
            return Ok(new { interviewId = interview.InterviewId, questions = initialQuestions.Select(q => new { q.QuestionId, q.QuestionText }) });
        }

        [HttpPost("chat/answer-next")]
        public async Task<IActionResult> SubmitChatAnswerAndGetFollowup([FromBody] TextAnswerNextDto dto)
        {
            // Find interview and question
            var interview = _db.Interviews.Include(i => i.Questions).Include(i => i.UserAnswers).FirstOrDefault(i => i.InterviewId == dto.InterviewId && i.InterviewType == InterviewType.Chat);
            if (interview == null) return NotFound("Interview not found.");
            var question = interview.Questions.FirstOrDefault(q => q.QuestionId == dto.QuestionId);
            if (question == null) return NotFound("Question not found.");
            // Save or update answer
            var answer = interview.UserAnswers.FirstOrDefault(a => a.QuestionId == dto.QuestionId);
            if (answer == null)
            {
                answer = new UserAnswer
                {
                    InterviewId = dto.InterviewId,
                    QuestionId = dto.QuestionId,
                    UserAnswerText = dto.UserAnswerText
                };
                _db.UserAnswers.Add(answer);
            }
            else
            {
                answer.UserAnswerText = dto.UserAnswerText;
            }
            await _db.SaveChangesAsync();
            // Generate follow-up for this question branch
            var payload = new
            {
                job_title = interview.CV?.JobTitle ?? "",
                level = question.DifficultyLevel.ToString().ToLower(),
                cv_text = interview.CV?.ExtractedText ?? "",
                previous_qa = new[] { new { question = question.QuestionText, answer = dto.UserAnswerText } },
                last_question = question.QuestionText,
                last_answer = dto.UserAnswerText
            };
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-followup", payload);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Failed to get follow-up question from AI service.");
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var followupText = result?["question"] ?? "";
            if (string.IsNullOrWhiteSpace(followupText))
                return Ok(new { message = "No follow-up question" });
            var followupQuestion = new Question
            {
                QuestionText = followupText,
                DifficultyLevel = question.DifficultyLevel,
                QuestionMark = 5,
                InterviewId = interview.InterviewId,
                ParentQuestionId = question.QuestionId
            };
            _db.Questions.Add(followupQuestion);
            await _db.SaveChangesAsync();
            return Ok(new { question = followupText, questionId = followupQuestion.QuestionId });
        }

        [HttpPost("chat/edit-answer")]
        public async Task<IActionResult> EditChatAnswerAndRegenerateBranch([FromBody] EditTextAnswerDto dto)
        {
            // Find interview and question
            var interview = _db.Interviews.Include(i => i.Questions).Include(i => i.UserAnswers).FirstOrDefault(i => i.InterviewId == dto.InterviewId && i.InterviewType == InterviewType.Chat);
            if (interview == null) return NotFound("Interview not found.");
            var question = interview.Questions.FirstOrDefault(q => q.QuestionId == dto.QuestionId);
            if (question == null) return NotFound("Question not found.");
            // Update answer
            var answer = interview.UserAnswers.FirstOrDefault(a => a.QuestionId == dto.QuestionId);
            if (answer == null)
                return NotFound("Answer not found.");
            answer.UserAnswerText = dto.NewAnswerText;
            await _db.SaveChangesAsync();
            // Delete all descendant follow-up questions and answers
            var toDeleteQuestions = new List<Question>();
            var toDeleteAnswers = new List<UserAnswer>();
            void CollectDescendants(int parentId)
            {
                var children = interview.Questions.Where(q => q.ParentQuestionId == parentId).ToList();
                foreach (var child in children)
                {
                    toDeleteQuestions.Add(child);
                    var childAnswer = interview.UserAnswers.FirstOrDefault(a => a.QuestionId == child.QuestionId);
                    if (childAnswer != null) toDeleteAnswers.Add(childAnswer);
                    CollectDescendants(child.QuestionId);
                }
            }
            CollectDescendants(dto.QuestionId);
            _db.UserAnswers.RemoveRange(toDeleteAnswers);
            _db.Questions.RemoveRange(toDeleteQuestions);
            await _db.SaveChangesAsync();
            // Generate new follow-up for the edited answer
            var payload = new
            {
                job_title = interview.CV?.JobTitle ?? "",
                level = question.DifficultyLevel.ToString().ToLower(),
                cv_text = interview.CV?.ExtractedText ?? "",
                previous_qa = new[] { new { question = question.QuestionText, answer = dto.NewAnswerText } },
                last_question = question.QuestionText,
                last_answer = dto.NewAnswerText
            };
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("https://7633-34-21-27-65.ngrok-free.app/generate-followup", payload);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Failed to get follow-up question from AI service.");
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var followupText = result?["question"] ?? "";
            if (string.IsNullOrWhiteSpace(followupText))
                return Ok(new { message = "No follow-up question" });
            var followupQuestion = new Question
            {
                QuestionText = followupText,
                DifficultyLevel = question.DifficultyLevel,
                QuestionMark = 5,
                InterviewId = interview.InterviewId,
                ParentQuestionId = question.QuestionId
            };
            _db.Questions.Add(followupQuestion);
            await _db.SaveChangesAsync();
            return Ok(new { question = followupText, questionId = followupQuestion.QuestionId });
        }
    }
}
