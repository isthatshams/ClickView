namespace ClickView.Models
{
    public class Answer
    {
        public int AnswerId { get; set; }
        public int UserId { get; set; }
        public int QuestionId { get; set; }
        public string UserAnswerText { get; set; }
        public string CorrectAnswerText { get; set; } // Stores the correct answer text

    }
}
