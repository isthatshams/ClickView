namespace ClickView.DTOs
{
    public class EvaluateAnswerDto
    {
        public int InterviewId { get; set; }
        public int QuestionId { get; set; }
        public string UserAnswerText { get; set; }
    }
}