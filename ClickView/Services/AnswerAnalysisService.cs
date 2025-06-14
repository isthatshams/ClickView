using System.Net.Http.Json;
using System.Text.Json;
using ClickView.DTOs;
using ClickView.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;

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
            try
            {
                if (answers == null || !answers.Any())
                {
                    Console.WriteLine("No answers provided for feedback generation");
                    return new FeedbackReportDto
                    {
                        Strengths = "No answers available for analysis",
                        Weaknesses = "No answers available for analysis",
                        PersonalitySummary = "No personality data available",
                        Recommendation = "Complete more interview questions to receive personalized feedback"
                    };
                }

                var payload = new
                {
                    answers = answers.Select(a => new {
                        question = a.Question?.QuestionText ?? "Unknown question",
                        answer = a.UserAnswerText ?? "",
                        tone = a.AnswerAnalysis?.Tone ?? "neutral",
                        personalityTraits = a.AnswerAnalysis?.PersonalityTraits != null
                            ? JsonSerializer.Deserialize<List<string>>(a.AnswerAnalysis.PersonalityTraits)
                            : new List<string>(),
                        softSkills = a.AnswerAnalysis?.SoftSkills != null
                            ? JsonSerializer.Deserialize<List<string>>(a.AnswerAnalysis.SoftSkills)
                            : new List<string>()
                    }).ToList()
                };

                Console.WriteLine($"Sending feedback request to AI service with {payload.answers.Count} answers");
                
                var response = await _client.PostAsJsonAsync("http://127.0.0.1:5000/generate-feedback", payload);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"AI service returned error: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"AI service error: {response.StatusCode} - {errorContent}");
                }
                
                var result = await response.Content.ReadFromJsonAsync<FeedbackReportDto>();
                
                if (result == null)
                {
                    Console.WriteLine("AI service returned null result");
                    throw new InvalidOperationException("AI service returned null result");
                }
                
                Console.WriteLine("Successfully generated feedback report");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateFeedbackReportAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                
                // Return a default feedback report instead of throwing
                return new FeedbackReportDto
                {
                    Strengths = "Unable to analyze strengths at this time",
                    Weaknesses = "Unable to analyze weaknesses at this time",
                    PersonalitySummary = "Unable to analyze personality at this time",
                    Recommendation = "Please try again later or contact support if the issue persists"
                };
            }
        }
    }
} 