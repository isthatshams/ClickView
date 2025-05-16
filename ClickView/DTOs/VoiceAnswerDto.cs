namespace ClickView.DTOs
{
    public class VoiceAnswerDto
    {
        public int QuestionId { get; set; }
        public IFormFile AudioFile { get; set; }
    }

}
