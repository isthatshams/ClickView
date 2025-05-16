namespace ClickView.DTOs
{
    public class StartInterviewDto
    {
        public int UserId { get; set; }
        public string InterviewType { get; set; } // "Chat" or "Voice"
        public int? CvId { get; set; }
        public string Level { get; set; }  // "internship", "junior", "mid", "senior"
        public string JobTitle { get; set; }
    }
}
