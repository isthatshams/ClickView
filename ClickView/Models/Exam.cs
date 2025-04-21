namespace ClickView.Models
{
    public class Exam
    {
        public int ExamId { get; set; }
        public ExamType ExamType { get; set; } // Chat or Voice
        public int UserId { get; set; }
        public List<Question> Questions { get; set; }

    }
}
