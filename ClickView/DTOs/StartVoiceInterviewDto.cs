namespace ClickView.DTOs
{
    public class StartVoiceInterviewDto
    {
        public int UserId { get; set; }
        public int? CvId { get; set; }
        public string Level { get; set; }
        public string JobTitle { get; set; }
    }
} 