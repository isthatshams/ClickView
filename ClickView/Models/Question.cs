namespace ClickView.Models
{
    public class Question
    {
        public int QuestionId { get; set; }
        public required string QuestionText { get; set; }
        public DifficultyLevel DifficultyLevel { get; set; }

    }
}
