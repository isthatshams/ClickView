using Microsoft.AspNetCore.Mvc;
using ClickView.Models;
using ClickView.DTOs;
using ClickView.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http.Json;
using ClickView.Services;

namespace ClickView.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InterviewController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AnswerAnalysisService _analysisService;
        private readonly InterviewScoringService _scoringService;

        public InterviewController(ApplicationDbContext db, AnswerAnalysisService analysisService, InterviewScoringService scoringService)
        {
            _db = db;
            _analysisService = analysisService;
            _scoringService = scoringService;
        }

        // Helper method to check if all questions in an interview have been answered
        private bool AreAllQuestionsAnswered(Interview interview)
        {
            var allQuestions = interview.Questions.ToList();
            var answeredQuestions = interview.UserAnswers.Select(a => a.QuestionId).ToHashSet();
            
            // Check if every question has a corresponding answer
            return allQuestions.All(q => answeredQuestions.Contains(q.QuestionId));
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

        // Retrieves full detailed data about a single interview 
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
                    StartedAt = i.StartedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"), // Return local time format
                    FinishedAt = i.FinishedAt.HasValue ? i.FinishedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff") : null,
                    i.IsFinished,
                    ScoreGrade = i.IsFinished ? _scoringService.GetScoreGrade(i.InterviewMark) : null,
                    ScoreFeedback = i.IsFinished ? _scoringService.GetScoreFeedback(i.InterviewMark) : null,
                    Questions = i.Questions.Select(q => new
                    {
                        q.QuestionId,
                        q.QuestionText,
                        q.DifficultyLevel,
                        q.QuestionMark,
                        q.ParentQuestionId
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

            // Log the start time being sent to frontend
            Console.WriteLine($"Sending interview {id} StartedAt to frontend: {interview.StartedAt}");
            Console.WriteLine($"Current local time: {DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff}");

            return Ok(interview);
        }
        // Returns a summary list of all interviews taken by a specific user
        [Authorize]
        [HttpGet("user/{userId}")]
        public IActionResult GetUserInterviews(int userId)
        {
            var user = _db.Users.Find(userId);
            if (user == null) return NotFound("User not found.");

            var interviews = _db.Interviews
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.StartedAt)
                .Select(i => new
                {
                    i.InterviewId,
                    i.InterviewType,
                    i.InterviewMark,
                    StartedAt = i.StartedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"), // Return local time format
                    FinishedAt = i.FinishedAt.HasValue ? i.FinishedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff") : null,
                    i.IsFinished,
                    ScoreGrade = i.IsFinished ? _scoringService.GetScoreGrade(i.InterviewMark) : null,
                    ScoreFeedback = i.IsFinished ? _scoringService.GetScoreFeedback(i.InterviewMark) : null,
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

            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-questions", payload);
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

            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-questions", payload);
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

            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-questions", payload);
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
            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-followup", requestBody);
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
            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-questions", payload);
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
            // Analyze answer
            var analysis = await _analysisService.AnalyzeAnswerAsync(dto.UserAnswerText);
            analysis.UserAnswerId = answer.UserAnswerId;
            _db.AnswerAnalyses.Add(analysis);
            answer.AnswerAnalysisId = analysis.AnswerAnalysisId;
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
            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-followup", payload);
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
                StartedAt = DateTime.UtcNow,
                Questions = new List<Question>(),
                UserAnswers = new List<UserAnswer>()
            };
            _db.Interviews.Add(interview);
            await _db.SaveChangesAsync();
            
            // Log the start time for debugging
            Console.WriteLine($"Interview {interview.InterviewId} started at: {interview.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Current UTC time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            // Generate initial batch of questions (3–5, diverse topics)
            var payload = new
            {
                level = dto.Level.ToLower(),
                cv_text = cv?.ExtractedText ?? "",
                topic = dto.JobTitle
            };
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-questions", payload);
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
            return Ok(new { 
                interviewId = interview.InterviewId, 
                questions = initialQuestions.Select(q => new { 
                    q.QuestionId, 
                    q.QuestionText,
                    q.DifficultyLevel,
                    q.QuestionMark,
                    q.ParentQuestionId
                }).ToList()
            });
        }

        [HttpPost("chat/answer-next")]
        public async Task<IActionResult> SubmitChatAnswerAndGetFollowup([FromBody] TextAnswerNextDto dto)
        {
            try
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
                    await _db.SaveChangesAsync();
                    // Analyze answer
                    var analysis = await _analysisService.AnalyzeAnswerAsync(dto.UserAnswerText);
                    analysis.UserAnswerId = answer.UserAnswerId;
                    _db.AnswerAnalyses.Add(analysis);
                    answer.AnswerAnalysisId = analysis.AnswerAnalysisId;
                    await _db.SaveChangesAsync();
                }
                else
                {
                    answer.UserAnswerText = dto.UserAnswerText;
                    await _db.SaveChangesAsync();
                    // Re-analyze answer
                    var analysis = await _analysisService.AnalyzeAnswerAsync(dto.UserAnswerText);
                    analysis.UserAnswerId = answer.UserAnswerId;
                    _db.AnswerAnalyses.Add(analysis);
                    answer.AnswerAnalysisId = analysis.AnswerAnalysisId;
                    await _db.SaveChangesAsync();
                }
                
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
                client.Timeout = TimeSpan.FromSeconds(30);
                
                var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-followup", payload);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Failed to get follow-up question from AI service: {errorContent}");
                }
                
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                
                // Handle the should_continue logic with proper type conversion
                var shouldContinueElement = result?.GetValueOrDefault("should_continue");
                var reason = result?.GetValueOrDefault("reason")?.ToString() ?? "";
                var followupText = result?.GetValueOrDefault("question")?.ToString() ?? "";
                
                // Safely convert should_continue to boolean
                bool shouldContinue = false;
                if (shouldContinueElement != null)
                {
                    if (shouldContinueElement is bool boolValue)
                    {
                        shouldContinue = boolValue;
                    }
                    else if (shouldContinueElement is string stringValue)
                    {
                        shouldContinue = bool.TryParse(stringValue, out bool parsed) && parsed;
                    }
                    else
                    {
                        // Try to convert using JsonElement
                        shouldContinue = shouldContinueElement.ToString().ToLower() == "true";
                    }
                }
                
                if (!shouldContinue)
                {
                    // AI determined the answer is sufficient - move to next main question
                    // Find the next main question (no parent question)
                    var allQuestions = interview.Questions.OrderBy(q => q.QuestionId).ToList();
                    var currentQuestionIndex = allQuestions.FindIndex(q => q.QuestionId == dto.QuestionId);
                    var nextMainQuestion = allQuestions
                        .Skip(currentQuestionIndex + 1)
                        .FirstOrDefault(q => q.ParentQuestionId == null);
                    
                    if (nextMainQuestion != null)
                    {
                        return Ok(new { 
                            should_continue = false, 
                            reason = reason,
                            message = "Answer accepted. Moving to next question.",
                            next_question_id = nextMainQuestion.QuestionId,
                            next_question_text = nextMainQuestion.QuestionText
                        });
                    }
                    else
                    {
                        // No more main questions - check if all questions are answered
                        if (AreAllQuestionsAnswered(interview))
                        {
                            // All questions answered - complete the interview
                            interview.IsFinished = true;
                            interview.FinishedAt = DateTime.Now;
                            await _db.SaveChangesAsync();
                            
                            // Automatically analyze answers and generate summary
                            try
                            {
                                // Generate AI summary
                                var aiSummary = await _analysisService.AnalyzeInterviewAsync(interview.UserAnswers.ToList());
                                
                                // Generate feedback report if it doesn't exist
                                var existingFeedback = _db.FeedbackReports.FirstOrDefault(f => f.InterviewId == interview.InterviewId);
                                if (existingFeedback == null)
                                {
                                    var feedbackDto = await _analysisService.GenerateFeedbackReportAsync(interview.UserAnswers.ToList());
                                    var feedbackReport = new FeedbackReport
                                    {
                                        InterviewId = interview.InterviewId,
                                        Strengths = feedbackDto.Strengths,
                                        Weaknesses = feedbackDto.Weaknesses,
                                        PersonalitySummary = feedbackDto.PersonalitySummary,
                                        Recommendation = feedbackDto.Recommendation
                                    };
                                    _db.FeedbackReports.Add(feedbackReport);
                                    await _db.SaveChangesAsync();
                                }
                            }
                            catch (Exception analysisEx)
                            {
                                // Log analysis error but don't fail the interview completion
                                Console.WriteLine($"Error during interview analysis: {analysisEx.Message}");
                            }
                            
                            return Ok(new { 
                                should_continue = false, 
                                reason = reason,
                                message = "Interview completed - all questions answered satisfactorily.",
                                interview_completed = true,
                                farewell_message = "Thank you, you've completed all questions. We will review your answers."
                            });
                        }
                        else
                        {
                            // Not all questions answered - continue with follow-ups or next questions
                            return Ok(new { 
                                should_continue = false, 
                                reason = "Answer accepted. Moving to next question.",
                                message = "Answer accepted. Moving to next question."
                            });
                        }
                    }
                }
                else if (shouldContinue && !string.IsNullOrWhiteSpace(followupText))
                {
                    // AI wants to continue with a follow-up question
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
                    
                    return Ok(new { 
                        should_continue = true, 
                        reason = reason,
                        question = followupText, 
                        questionId = followupQuestion.QuestionId 
                    });
                }
                else
                {
                    // Fallback: no follow-up question or should_continue not provided
                    return Ok(new { 
                        should_continue = false, 
                        reason = "No follow-up question generated",
                        message = "No follow-up question" 
                    });
                }
            }
            catch (Exception ex)
            {
                // Log the exception details
                Console.WriteLine($"Error in SubmitChatAnswerAndGetFollowup: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPost("chat/edit-answer")]
        public async Task<IActionResult> EditChatAnswerAndRegenerateBranch([FromBody] EditTextAnswerDto dto)
        {
            try
            {
                // Validate input
                if (dto == null)
                    return BadRequest("Request body is null");
                
                if (string.IsNullOrWhiteSpace(dto.NewAnswerText))
                    return BadRequest("NewAnswerText is required");

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
                client.Timeout = TimeSpan.FromSeconds(30); // Add timeout
                
                var response = await client.PostAsJsonAsync("http://127.0.0.1:5000/generate-followup", payload);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Failed to get follow-up question from AI service: {errorContent}");
                }
                
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                
                // Handle the should_continue logic with proper type conversion
                var shouldContinueElement = result?.GetValueOrDefault("should_continue");
                var reason = result?.GetValueOrDefault("reason")?.ToString() ?? "";
                var followupText = result?.GetValueOrDefault("question")?.ToString() ?? "";
                
                // Safely convert should_continue to boolean
                bool shouldContinue = false;
                if (shouldContinueElement != null)
                {
                    if (shouldContinueElement is bool boolValue)
                    {
                        shouldContinue = boolValue;
                    }
                    else if (shouldContinueElement is string stringValue)
                    {
                        shouldContinue = bool.TryParse(stringValue, out bool parsed) && parsed;
                    }
                    else
                    {
                        // Try to convert using JsonElement
                        shouldContinue = shouldContinueElement.ToString().ToLower() == "true";
                    }
                }
                
                if (!shouldContinue)
                {
                    // AI determined the answer is sufficient - move to next main question
                    // Find the next main question (no parent question)
                    var allQuestions = interview.Questions.OrderBy(q => q.QuestionId).ToList();
                    var currentQuestionIndex = allQuestions.FindIndex(q => q.QuestionId == dto.QuestionId);
                    var nextMainQuestion = allQuestions
                        .Skip(currentQuestionIndex + 1)
                        .FirstOrDefault(q => q.ParentQuestionId == null);
                    
                    if (nextMainQuestion != null)
                    {
                        return Ok(new { 
                            should_continue = false, 
                            reason = reason,
                            message = "Answer accepted. Moving to next question.",
                            next_question_id = nextMainQuestion.QuestionId,
                            next_question_text = nextMainQuestion.QuestionText
                        });
                    }
                    else
                    {
                        // No more main questions - check if all questions are answered
                        if (AreAllQuestionsAnswered(interview))
                        {
                            // All questions answered - complete the interview
                            interview.IsFinished = true;
                            interview.FinishedAt = DateTime.Now;
                            await _db.SaveChangesAsync();
                            
                            // Automatically analyze answers and generate summary
                            try
                            {
                                // Generate AI summary
                                var aiSummary = await _analysisService.AnalyzeInterviewAsync(interview.UserAnswers.ToList());
                                
                                // Generate feedback report if it doesn't exist
                                var existingFeedback = _db.FeedbackReports.FirstOrDefault(f => f.InterviewId == interview.InterviewId);
                                if (existingFeedback == null)
                                {
                                    var feedbackDto = await _analysisService.GenerateFeedbackReportAsync(interview.UserAnswers.ToList());
                                    var feedbackReport = new FeedbackReport
                                    {
                                        InterviewId = interview.InterviewId,
                                        Strengths = feedbackDto.Strengths,
                                        Weaknesses = feedbackDto.Weaknesses,
                                        PersonalitySummary = feedbackDto.PersonalitySummary,
                                        Recommendation = feedbackDto.Recommendation
                                    };
                                    _db.FeedbackReports.Add(feedbackReport);
                                    await _db.SaveChangesAsync();
                                }
                            }
                            catch (Exception analysisEx)
                            {
                                // Log analysis error but don't fail the interview completion
                                Console.WriteLine($"Error during interview analysis: {analysisEx.Message}");
                            }
                            
                            return Ok(new { 
                                should_continue = false, 
                                reason = reason,
                                message = "Interview completed - all questions answered satisfactorily.",
                                interview_completed = true,
                                farewell_message = "Thank you, you've completed all questions. We will review your answers."
                            });
                        }
                        else
                        {
                            // Not all questions answered - continue with follow-ups or next questions
                            return Ok(new { 
                                should_continue = false, 
                                reason = "Answer accepted. Moving to next question.",
                                message = "Answer accepted. Moving to next question."
                            });
                        }
                    }
                }
                else if (shouldContinue && !string.IsNullOrWhiteSpace(followupText))
                {
                    // AI wants to continue with a follow-up question
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
                    
                    return Ok(new { 
                        should_continue = true, 
                        reason = reason,
                        question = followupText, 
                        questionId = followupQuestion.QuestionId 
                    });
                }
                else
                {
                    // Fallback: no follow-up question or should_continue not provided
                    return Ok(new { 
                        should_continue = false, 
                        reason = "No follow-up question generated",
                        message = "No follow-up question" 
                    });
                }
            }
            catch (Exception ex)
            {
                // Log the exception details
                Console.WriteLine($"Error in EditChatAnswerAndRegenerateBranch: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpGet("{interviewId}/summary/ai")]
        public async Task<IActionResult> GetInterviewAISummary(int interviewId)
        {
            var interview = _db.Interviews
                .Include(i => i.UserAnswers)
                .ThenInclude(a => a.AnswerAnalysis)
                .Include(i => i.Questions)
                .FirstOrDefault(i => i.InterviewId == interviewId);
            if (interview == null) return NotFound("Interview not found.");
            var summary = await _analysisService.AnalyzeInterviewAsync(interview.UserAnswers.ToList());
            return Ok(summary);
        }

        [HttpGet("{interviewId}/feedback")]
        public async Task<IActionResult> GetInterviewFeedback(int interviewId)
        {
            try
            {
                var interview = _db.Interviews
                    .Include(i => i.UserAnswers)
                    .ThenInclude(a => a.AnswerAnalysis)
                    .Include(i => i.Questions)
                    .FirstOrDefault(i => i.InterviewId == interviewId);
                
                if (interview == null) 
                {
                    Console.WriteLine($"Interview not found for ID: {interviewId}");
                    return NotFound("Interview not found.");
                }

                // Check if feedback already exists
                var existing = _db.FeedbackReports.FirstOrDefault(f => f.InterviewId == interviewId);
                if (existing != null)
                {
                    Console.WriteLine($"Returning existing feedback for interview ID: {interviewId}");
                    return Ok(existing);
                }

                // Check if there are any answers to analyze
                if (!interview.UserAnswers.Any())
                {
                    Console.WriteLine($"No answers found for interview ID: {interviewId}");
                    return BadRequest("No answers found to generate feedback from.");
                }

                Console.WriteLine($"Generating new feedback for interview ID: {interviewId} with {interview.UserAnswers.Count} answers");
                
                // Generate feedback
                var feedbackDto = await _analysisService.GenerateFeedbackReportAsync(interview.UserAnswers.ToList());
                
                if (feedbackDto == null)
                {
                    Console.WriteLine($"Failed to generate feedback DTO for interview ID: {interviewId}");
                    return StatusCode(500, new { error = "Failed to generate feedback report" });
                }

                var report = new FeedbackReport
                {
                    InterviewId = interviewId,
                    Strengths = feedbackDto.Strengths ?? "No strengths identified",
                    Weaknesses = feedbackDto.Weaknesses ?? "No weaknesses identified",
                    PersonalitySummary = feedbackDto.PersonalitySummary ?? "No personality summary available",
                    Recommendation = feedbackDto.Recommendation ?? "No recommendation available"
                };

                _db.FeedbackReports.Add(report);
                await _db.SaveChangesAsync();
                
                Console.WriteLine($"Successfully created feedback report for interview ID: {interviewId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetInterviewFeedback for interview ID {interviewId}: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { 
                    error = "Error generating feedback report", 
                    details = ex.Message,
                    interviewId = interviewId
                });
            }
        }

        [HttpPost("{interviewId}/end")]
        public async Task<IActionResult> EndInterview(int interviewId)
        {
            var interview = await _db.Interviews
                .Include(i => i.UserAnswers)
                .ThenInclude(a => a.AnswerAnalysis)
                .Include(i => i.Questions)
                .FirstOrDefaultAsync(i => i.InterviewId == interviewId);
                
            if (interview == null)
                return NotFound("Interview not found.");

            if (interview.IsFinished)
            {
                // Interview is already finished, return success instead of error
                return Ok(new { 
                    message = "Interview was already finished", 
                    interviewId = interview.InterviewId, 
                    finishedAt = interview.FinishedAt,
                    interviewMark = interview.InterviewMark
                });
            }

            try
            {
                // Mark interview as finished
                interview.IsFinished = true;
                interview.FinishedAt = DateTime.Now;

                // Calculate interview score if there are answers
                if (interview.UserAnswers.Any())
                {
                    try
                    {
                        var calculatedScore = await _scoringService.CalculateInterviewScoreAsync(interview);
                        interview.InterviewMark = calculatedScore;
                        
                        Console.WriteLine($"Interview {interviewId} scored: {calculatedScore:F1}");
                    }
                    catch (Exception scoreEx)
                    {
                        Console.WriteLine($"Error calculating interview score: {scoreEx.Message}");
                        // Continue without scoring if it fails
                    }
                }

                await _db.SaveChangesAsync();

                // Automatically analyze answers and generate summary if there are answers
                if (interview.UserAnswers.Any())
                {
                    try
                    {
                        // Generate AI summary
                        var aiSummary = await _analysisService.AnalyzeInterviewAsync(interview.UserAnswers.ToList());
                        
                        // Generate feedback report if it doesn't exist
                        var existingFeedback = _db.FeedbackReports.FirstOrDefault(f => f.InterviewId == interviewId);
                        if (existingFeedback == null)
                        {
                            var feedbackDto = await _analysisService.GenerateFeedbackReportAsync(interview.UserAnswers.ToList());
                            var feedbackReport = new FeedbackReport
                            {
                                InterviewId = interviewId,
                                Strengths = feedbackDto.Strengths,
                                Weaknesses = feedbackDto.Weaknesses,
                                PersonalitySummary = feedbackDto.PersonalitySummary,
                                Recommendation = feedbackDto.Recommendation
                            };
                            _db.FeedbackReports.Add(feedbackReport);
                            await _db.SaveChangesAsync();
                        }

                        return Ok(new { 
                            message = "Interview ended successfully with analysis completed", 
                            interviewId = interview.InterviewId, 
                            finishedAt = interview.FinishedAt,
                            interviewMark = interview.InterviewMark,
                            scoreGrade = _scoringService.GetScoreGrade(interview.InterviewMark),
                            scoreFeedback = _scoringService.GetScoreFeedback(interview.InterviewMark),
                            analysisCompleted = true,
                            aiSummary = aiSummary
                        });
                    }
                    catch (Exception analysisEx)
                    {
                        // Log analysis error but don't fail the interview ending
                        Console.WriteLine($"Error during interview analysis: {analysisEx.Message}");
                        return Ok(new { 
                            message = "Interview ended successfully (analysis failed)", 
                            interviewId = interview.InterviewId, 
                            finishedAt = interview.FinishedAt,
                            interviewMark = interview.InterviewMark,
                            scoreGrade = _scoringService.GetScoreGrade(interview.InterviewMark),
                            scoreFeedback = _scoringService.GetScoreFeedback(interview.InterviewMark),
                            analysisCompleted = false,
                            analysisError = analysisEx.Message
                        });
                    }
                }
                else
                {
                    // No answers to analyze
                    return Ok(new { 
                        message = "Interview ended successfully (no answers to analyze)", 
                        interviewId = interview.InterviewId, 
                        finishedAt = interview.FinishedAt,
                        interviewMark = interview.InterviewMark,
                        scoreGrade = _scoringService.GetScoreGrade(interview.InterviewMark),
                        scoreFeedback = _scoringService.GetScoreFeedback(interview.InterviewMark),
                        analysisCompleted = false
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error ending interview", 
                    details = ex.Message 
                });
            }
        }

        [HttpPost("check-expiration")]
        public async Task<IActionResult> CheckAndExpireInterviews()
        {
            try
            {
                var cutoffTime = DateTime.Now.Subtract(TimeSpan.FromMinutes(45));
                
                // Find interviews that started more than 45 minutes ago and are not finished
                var expiredInterviews = await _db.Interviews
                    .Where(i => i.StartedAt <= cutoffTime && !i.IsFinished)
                    .ToListAsync();

                if (expiredInterviews.Any())
                {
                    foreach (var interview in expiredInterviews)
                    {
                        interview.IsFinished = true;
                        interview.FinishedAt = DateTime.Now;
                    }

                    await _db.SaveChangesAsync();
                    
                    return Ok(new { 
                        message = $"Successfully expired {expiredInterviews.Count} interviews",
                        expiredCount = expiredInterviews.Count,
                        expiredInterviews = expiredInterviews.Select(i => new { i.InterviewId, i.StartedAt, i.FinishedAt })
                    });
                }

                return Ok(new { message = "No interviews to expire", expiredCount = 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error checking interview expiration", details = ex.Message });
            }
        }

        [HttpGet("{interviewId}/score-breakdown")]
        public async Task<IActionResult> GetInterviewScoreBreakdown(int interviewId)
        {
            try
            {
                var interview = await _db.Interviews
                    .Include(i => i.UserAnswers)
                    .ThenInclude(a => a.AnswerAnalysis)
                    .Include(i => i.Questions)
                    .FirstOrDefaultAsync(i => i.InterviewId == interviewId);

                if (interview == null)
                    return NotFound("Interview not found.");

                if (!interview.IsFinished)
                    return BadRequest("Interview is not finished yet.");

                if (!interview.UserAnswers.Any())
                    return BadRequest("No answers found for this interview.");

                // Calculate score components
                var completionScore = _scoringService.CalculateCompletionScore(interview);
                var qualityScore = await _scoringService.CalculateAnswerQualityScoreAsync(interview);
                var difficultyScore = _scoringService.CalculateDifficultyScore(interview);
                var timeScore = _scoringService.CalculateTimeEfficiencyScore(interview);

                var breakdown = new
                {
                    InterviewId = interviewId,
                    TotalScore = interview.InterviewMark,
                    Grade = _scoringService.GetScoreGrade(interview.InterviewMark),
                    Feedback = _scoringService.GetScoreFeedback(interview.InterviewMark),
                    ScoreComponents = new
                    {
                        Completion = new
                        {
                            Score = completionScore,
                            Weight = 0.30,
                            WeightedScore = completionScore * 0.30,
                            Description = "Percentage of questions answered"
                        },
                        Quality = new
                        {
                            Score = qualityScore,
                            Weight = 0.40,
                            WeightedScore = qualityScore * 0.40,
                            Description = "Average quality of answers based on length, tone, and personality traits"
                        },
                        Difficulty = new
                        {
                            Score = difficultyScore,
                            Weight = 0.20,
                            WeightedScore = difficultyScore * 0.20,
                            Description = "Bonus for answering higher difficulty questions"
                        },
                        TimeEfficiency = new
                        {
                            Score = timeScore,
                            Weight = 0.10,
                            WeightedScore = timeScore * 0.10,
                            Description = "Efficiency in completing the interview"
                        }
                    },
                    InterviewDetails = new
                    {
                        TotalQuestions = interview.Questions.Count,
                        AnsweredQuestions = interview.UserAnswers.Count,
                        CompletionRate = (double)interview.UserAnswers.Count / interview.Questions.Count * 100,
                        Duration = interview.FinishedAt.HasValue ? 
                            (interview.FinishedAt.Value - interview.StartedAt).TotalMinutes : 0,
                        AverageTimePerAnswer = interview.FinishedAt.HasValue && interview.UserAnswers.Count > 0 ?
                            (interview.FinishedAt.Value - interview.StartedAt).TotalMinutes / interview.UserAnswers.Count : 0
                    }
                };

                return Ok(breakdown);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting score breakdown for interview {interviewId}: {ex.Message}");
                return StatusCode(500, new { error = "Error calculating score breakdown", details = ex.Message });
            }
        }

        [HttpPost("calculate-missing-scores")]
        public async Task<IActionResult> CalculateMissingScores()
        {
            try
            {
                // Find all completed interviews that have no score (InterviewMark = 0) but have answers
                var interviewsToScore = await _db.Interviews
                    .Include(i => i.UserAnswers)
                    .ThenInclude(a => a.AnswerAnalysis)
                    .Include(i => i.Questions)
                    .Where(i => i.IsFinished && i.InterviewMark == 0 && i.UserAnswers.Any())
                    .ToListAsync();

                if (!interviewsToScore.Any())
                {
                    return Ok(new { 
                        message = "No interviews found that need scoring", 
                        processedCount = 0 
                    });
                }

                var processedCount = 0;
                var errors = new List<string>();

                foreach (var interview in interviewsToScore)
                {
                    try
                    {
                        var calculatedScore = await _scoringService.CalculateInterviewScoreAsync(interview);
                        interview.InterviewMark = calculatedScore;
                        processedCount++;

                        Console.WriteLine($"Calculated score for interview {interview.InterviewId}: {calculatedScore:F1}");
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Error calculating score for interview {interview.InterviewId}: {ex.Message}";
                        errors.Add(errorMsg);
                        Console.WriteLine(errorMsg);
                    }
                }

                // Save all changes
                await _db.SaveChangesAsync();

                return Ok(new { 
                    message = $"Successfully calculated scores for {processedCount} interviews",
                    processedCount = processedCount,
                    totalFound = interviewsToScore.Count,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error calculating missing scores", 
                    details = ex.Message 
                });
            }
        }

        [HttpPost("calculate-score/{interviewId}")]
        public async Task<IActionResult> CalculateScoreForInterview(int interviewId)
        {
            try
            {
                var interview = await _db.Interviews
                    .Include(i => i.UserAnswers)
                    .ThenInclude(a => a.AnswerAnalysis)
                    .Include(i => i.Questions)
                    .FirstOrDefaultAsync(i => i.InterviewId == interviewId);

                if (interview == null)
                    return NotFound("Interview not found.");

                if (!interview.IsFinished)
                    return BadRequest("Interview is not finished yet.");

                if (!interview.UserAnswers.Any())
                    return BadRequest("No answers found for this interview.");

                var calculatedScore = await _scoringService.CalculateInterviewScoreAsync(interview);
                interview.InterviewMark = calculatedScore;
                await _db.SaveChangesAsync();

                return Ok(new { 
                    message = "Score calculated successfully",
                    interviewId = interviewId,
                    score = calculatedScore,
                    grade = _scoringService.GetScoreGrade(calculatedScore),
                    feedback = _scoringService.GetScoreFeedback(calculatedScore)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error calculating score", 
                    details = ex.Message 
                });
            }
        }
    }
}
