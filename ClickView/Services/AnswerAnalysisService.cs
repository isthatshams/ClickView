using System.Net.Http.Json;
using System.Text.Json;
using ClickView.DTOs;
using ClickView.Models;
using System.Threading.Tasks;
using System.Linq;

namespace ClickView.Services
{
    public class AnswerAnalysisService
    {
        private readonly HttpClient _client;
        public AnswerAnalysisService(HttpClient client)
        {
            _client = client;
        }

        public async Task<AnswerAnalysis> AnalyzeAnswerAsync(string answerText)
        {
            var payload = new { answer = answerText };
            var response = await _client.PostAsJsonAsync("http://127.0.0.1:5000/analyze-answer", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AnswerAnalysisDto>();
            return new AnswerAnalysis
            {
                Tone = result.Tone,
                PersonalityTraits = JsonSerializer.Serialize(result.PersonalityTraits),
                SoftSkills = JsonSerializer.Serialize(result.SoftSkills)
            };
        }

        public async Task<InterviewSummaryDto> AnalyzeInterviewAsync(List<UserAnswer> answers)
        {
            var payload = new
            {
                answers = answers.Select(a => new {
                    question = a.Question.QuestionText,
                    answer = a.UserAnswerText,
                    tone = a.AnswerAnalysis?.Tone,
                    personalityTraits = a.AnswerAnalysis?.PersonalityTraits != null
                        ? JsonSerializer.Deserialize<List<string>>(a.AnswerAnalysis.PersonalityTraits)
                        : new List<string>(),
                    softSkills = a.AnswerAnalysis?.SoftSkills != null
                        ? JsonSerializer.Deserialize<List<string>>(a.AnswerAnalysis.SoftSkills)
                        : new List<string>()
                })
            };
            var response = await _client.PostAsJsonAsync("http://127.0.0.1:5000/analyze-interview", payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InterviewSummaryDto>();
        }

        public async Task<FeedbackReportDto> GenerateFeedbackReportAsync(List<UserAnswer> answers)
        {
            var payload = new
            {
                answers = answers.Select(a => new {
                    question = a.Question.QuestionText,
                    answer = a.UserAnswerText,
                    tone = a.AnswerAnalysis?.Tone,
                    personalityTraits = a.AnswerAnalysis?.PersonalityTraits != null
                        ? JsonSerializer.Deserialize<List<string>>(a.AnswerAnalysis.PersonalityTraits)
                        : new List<string>(),
                    softSkills = a.AnswerAnalysis?.SoftSkills != null
                        ? JsonSerializer.Deserialize<List<string>>(a.AnswerAnalysis.SoftSkills)
                        : new List<string>()
                })
            };
            var response = await _client.PostAsJsonAsync("http://127.0.0.1:5000/generate-feedback", payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FeedbackReportDto>();
        }
    }
} 