using Microsoft.AspNetCore.Http;

namespace ClickView.DTOs
{
    public class VoiceAnswerDto
    {
        public int InterviewId { get; set; }
        public int QuestionId { get; set; }
        public string UserAnswerText { get; set; }
        public IFormFile AudioFile { get; set; }
    }
}
