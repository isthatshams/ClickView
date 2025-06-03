using System.Net.Http.Json;
using ClickView.DTOs;
using System.Threading.Tasks;

namespace ClickView.Services
{
    public class CvEnhancerService
    {
        private readonly HttpClient _client;
        public CvEnhancerService(HttpClient client)
        {
            _client = client;
        }

        public async Task<CvEnhancementDto> EnhanceCvAsync(string cvText, string jobTitle = null)
        {
            try
            {
                var prompt = string.IsNullOrWhiteSpace(jobTitle)
                    ? $"Suggest improvements for the following CV:\n\n{cvText}"
                    : $"Enhance and rewrite this CV to better match the role of {jobTitle}:\n\n{cvText}";
                
                var payload = new { 
                    cv_text = cvText,
                    job_title = jobTitle
                };

                var response = await _client.PostAsJsonAsync("http://127.0.0.1:5000/enhance-cv", payload);
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<CvEnhancementDto>();
                if (result == null)
                {
                    throw new Exception("Failed to parse enhancement response");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CvEnhancerService: {ex}");
                throw;
            }
        }
    }
} 