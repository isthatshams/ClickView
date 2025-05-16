namespace ClickView.DTOs
{
    public class AnswerQuestionDto
    {
        public int InterviewId { get; set; }
        public int QuestionId { get; set; }
        public string UserAnswerText { get; set; }
        public string? Notes { get; set; }
    }
}
