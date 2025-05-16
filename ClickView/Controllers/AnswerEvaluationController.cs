using ClickView.Data;
using ClickView.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClickView.DTOs;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;


namespace ClickView.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnswerEvaluationController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;

        public AnswerEvaluationController(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("evaluate")]
        public async Task<IActionResult> EvaluateAnswer(EvaluateAnswerDto dto)
        {
            var question = await _db.Questions.FindAsync(dto.QuestionId);
            if (question == null) return NotFound("Question not found.");

            var interview = await _db.Interviews.FindAsync(dto.InterviewId);
            if (interview == null) return NotFound("Interview not found.");

            var evaluation = await GetFeedbackFromChatGPT(
                question.QuestionText,
                dto.UserAnswerText,
                question.DifficultyLevel.ToString()
            );

            var userAnswer = new UserAnswer
            {
                QuestionId = dto.QuestionId,
                InterviewId = dto.InterviewId,
                UserAnswerText = dto.UserAnswerText,
                UserAnswerNotes = "",
                EvaluationScore = evaluation.Score,
                EvaluationFeedback = evaluation.Feedback
            };

            _db.UserAnswers.Add(userAnswer);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Answer saved and evaluated.",
                score = evaluation.Score,
                feedback = evaluation.Feedback
            });
        }

        private async Task<EvaluationResult> GetFeedbackFromChatGPT(string question, string answer, string level)
        {
            var client = _httpClientFactory.CreateClient();

            var payload = new
            {
                question = question,
                answer = answer,
                level = level
            };

            var response = await client.PostAsJsonAsync("https://your-ngrok-url.ngrok.io/evaluate-answer", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EvaluationResult>();
            return result!;
        }
        [HttpGet("interview/{interviewId}/summary")]
        public async Task<IActionResult> GetInterviewSummary(int interviewId)
        {
            var interview = await _db.Interviews.FindAsync(interviewId);
            if (interview == null) return NotFound("Interview not found.");

            var answers = await _db.UserAnswers
                .Where(a => a.InterviewId == interviewId)
                .Include(a => a.Question)
                .ToListAsync();

            var summary = answers.Select(a => new InterviewAnswerSummaryDto
            {
                QuestionText = a.Question?.QuestionText ?? "N/A",
                UserAnswerText = a.UserAnswerText,
                Score = a.EvaluationScore,
                Feedback = a.EvaluationFeedback ?? "No feedback"
            }).ToList();

            var averageScore = summary
                .Where(a => a.Score.HasValue)
                .Select(a => a.Score.Value)
                .DefaultIfEmpty(0)
                .Average();

            return Ok(new
            {
                interviewId,
                totalQuestions = summary.Count,
                averageScore,
                answers = summary
            });
        }
        [HttpPost("interview/{interviewId}/voice-answer")]
        public async Task<IActionResult> SubmitVoiceAnswer(int interviewId, [FromForm] VoiceAnswerDto dto)
        {
            var interview = await _db.Interviews.FindAsync(interviewId);
            if (interview == null) return NotFound("Interview not found.");

            if (interview.InterviewType != InterviewType.Voice)
                return BadRequest("This interview only accepts voice answers.");

            var question = await _db.Questions.FindAsync(dto.QuestionId);
            if (question == null) return NotFound("Question not found.");

            if (dto.AudioFile == null || dto.AudioFile.Length == 0)
                return BadRequest("No audio file uploaded.");

            byte[] audioBytes;
            using (var ms = new MemoryStream())
            {
                await dto.AudioFile.CopyToAsync(ms);
                audioBytes = ms.ToArray();
            }

            string transcript;
            try
            {
                transcript = await TranscribeAudioWithOpenAI(dto.AudioFile);
            }
            catch (Exception ex)
            {
                return BadRequest($"Transcription failed: {ex.Message}");
            }

            var evaluation = await GetFeedbackFromChatGPT(
                question.QuestionText,
                transcript,
                question.DifficultyLevel.ToString()
            );

            var userAnswer = new UserAnswer
            {
                QuestionId = dto.QuestionId,
                InterviewId = interviewId,
                UserAnswerText = "[Voice Answer]",
                AudioData = audioBytes,
                TranscribedText = transcript,
                EvaluationScore = evaluation.Score,
                EvaluationFeedback = evaluation.Feedback
            };

            _db.UserAnswers.Add(userAnswer);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Voice answer saved, transcribed, and evaluated.",
                score = evaluation.Score,
                feedback = evaluation.Feedback,
                transcript
            });
        }
        private async Task<string> TranscribeAudioWithOpenAI(IFormFile audio)
        {
            using var content = new MultipartFormDataContent();
            using var stream = audio.OpenReadStream();

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm"); // or audio/mp3
            content.Add(fileContent, "file", audio.FileName);
            content.Add(new StringContent("whisper-1"), "model");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "sk-your-openai-api-key");

            var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return result?["text"] ?? throw new Exception("No transcription returned.");
        }

    }
}
