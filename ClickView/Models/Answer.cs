namespace ClickView.Models
{
    public class Answer
    {
        public int AnswerId { get; set; }
        public int UserId { get; set; }
        public int QuestionId { get; set; }
        public string UserAnswerText { get; set; } // Stores the User's answer text
        public string CorrectAnswerText { get; set; } // Stores the correct answer text

    }
}
