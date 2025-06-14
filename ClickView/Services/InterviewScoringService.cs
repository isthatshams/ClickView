using ClickView.Models;
using System.Text.Json;

namespace ClickView.Services
{
    public class InterviewScoringService
    {
        public async Task<double> CalculateInterviewScoreAsync(Interview interview)
        {
            try
            {
                if (interview == null || !interview.UserAnswers.Any())
                {
                    Console.WriteLine($"No answers found for interview {interview?.InterviewId}");
                    return 0.0;
                }

                var scoreComponents = new List<double>();

                // 1. Completion Rate (30% of total score)
                var completionScore = CalculateCompletionScore(interview);
                scoreComponents.Add(completionScore * 0.30);

                // 2. Answer Quality Score (40% of total score)
                var qualityScore = await CalculateAnswerQualityScoreAsync(interview);
                scoreComponents.Add(qualityScore * 0.40);

                // 3. Difficulty Bonus (20% of total score)
                var difficultyScore = CalculateDifficultyScore(interview);
                scoreComponents.Add(difficultyScore * 0.20);

                // 4. Time Efficiency (10% of total score)
                var timeScore = CalculateTimeEfficiencyScore(interview);
                scoreComponents.Add(timeScore * 0.10);

                // Calculate total score
                var totalScore = scoreComponents.Sum();
                
                // Ensure score is between 0 and 100
                totalScore = Math.Max(0, Math.Min(100, totalScore));

                Console.WriteLine($"Interview {interview.InterviewId} scoring breakdown:");
                Console.WriteLine($"- Completion: {completionScore:F1} (30% weight: {completionScore * 0.30:F1})");
                Console.WriteLine($"- Quality: {qualityScore:F1} (40% weight: {qualityScore * 0.40:F1})");
                Console.WriteLine($"- Difficulty: {difficultyScore:F1} (20% weight: {difficultyScore * 0.20:F1})");
                Console.WriteLine($"- Time: {timeScore:F1} (10% weight: {timeScore * 0.10:F1})");
                Console.WriteLine($"- Total Score: {totalScore:F1}");

                return totalScore;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating interview score: {ex.Message}");
                return 0.0;
            }
        }

        public double CalculateCompletionScore(Interview interview)
        {
            var totalQuestions = interview.Questions.Count;
            var answeredQuestions = interview.UserAnswers.Count;

            if (totalQuestions == 0) return 0.0;

            var completionRate = (double)answeredQuestions / totalQuestions;
            return completionRate * 100; // Convert to percentage
        }

        public async Task<double> CalculateAnswerQualityScoreAsync(Interview interview)
        {
            var qualityScores = new List<double>();

            foreach (var answer in interview.UserAnswers)
            {
                var answerScore = await CalculateIndividualAnswerScoreAsync(answer);
                qualityScores.Add(answerScore);
            }

            return qualityScores.Any() ? qualityScores.Average() : 0.0;
        }

        private async Task<double> CalculateIndividualAnswerScoreAsync(UserAnswer answer)
        {
            var baseScore = 70.0; // Base score for providing an answer

            // 1. Answer Length Analysis (0-10 points)
            var lengthScore = CalculateLengthScore(answer.UserAnswerText);
            baseScore += lengthScore;

            // 2. Tone Analysis (0-10 points)
            var toneScore = CalculateToneScore(answer.AnswerAnalysis?.Tone);
            baseScore += toneScore;

            // 3. Personality Traits Analysis (0-10 points)
            var personalityScore = CalculatePersonalityScore(answer.AnswerAnalysis?.PersonalityTraits);
            baseScore += personalityScore;

            return Math.Min(100, baseScore); // Cap at 100
        }

        private double CalculateLengthScore(string answerText)
        {
            if (string.IsNullOrWhiteSpace(answerText)) return 0.0;

            var wordCount = answerText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            
            // Score based on word count (optimal range: 50-200 words)
            if (wordCount < 10) return 2.0;
            if (wordCount < 25) return 5.0;
            if (wordCount < 50) return 8.0;
            if (wordCount < 100) return 10.0;
            if (wordCount < 200) return 9.0;
            if (wordCount < 300) return 7.0;
            return 5.0; // Very long answers might be verbose
        }

        private double CalculateToneScore(string? tone)
        {
            if (string.IsNullOrEmpty(tone)) return 5.0; // Neutral score for unknown tone

            return tone.ToLower() switch
            {
                "confident" => 10.0,
                "enthusiastic" => 9.0,
                "analytical" => 8.0,
                "professional" => 8.0,
                "neutral" => 6.0,
                "hesitant" => 4.0,
                "passive" => 3.0,
                _ => 5.0 // Default for unknown tones
            };
        }

        private double CalculatePersonalityScore(string? personalityTraitsJson)
        {
            if (string.IsNullOrEmpty(personalityTraitsJson)) return 5.0;

            try
            {
                var traits = JsonSerializer.Deserialize<List<string>>(personalityTraitsJson);
                if (traits == null || !traits.Any()) return 5.0;

                var positiveTraits = new[] { "analytical", "empathetic", "assertive", "confident", "professional", "detail-oriented", "collaborative" };
                var negativeTraits = new[] { "passive", "hesitant", "aggressive", "disorganized" };

                var score = 5.0; // Base score

                foreach (var trait in traits)
                {
                    if (positiveTraits.Contains(trait.ToLower()))
                        score += 1.5;
                    else if (negativeTraits.Contains(trait.ToLower()))
                        score -= 1.0;
                }

                return Math.Max(0, Math.Min(10, score));
            }
            catch
            {
                return 5.0; // Default score if parsing fails
            }
        }

        public double CalculateDifficultyScore(Interview interview)
        {
            var difficultyScores = new List<double>();

            foreach (var answer in interview.UserAnswers)
            {
                var question = interview.Questions.FirstOrDefault(q => q.QuestionId == answer.QuestionId);
                if (question != null)
                {
                    var difficultyBonus = question.DifficultyLevel switch
                    {
                        DifficultyLevel.Internship => 5.0,  // Intern level
                        DifficultyLevel.Junior => 10.0,     // Junior level
                        DifficultyLevel.Mid => 15.0,        // Mid level
                        DifficultyLevel.Senior => 20.0,     // Senior level
                        _ => 5.0   // Default
                    };
                    difficultyScores.Add(difficultyBonus);
                }
            }

            return difficultyScores.Any() ? difficultyScores.Average() : 0.0;
        }

        public double CalculateTimeEfficiencyScore(Interview interview)
        {
            if (!interview.FinishedAt.HasValue || interview.UserAnswers.Count == 0)
                return 0.0;

            var totalTimeMinutes = (interview.FinishedAt.Value - interview.StartedAt).TotalMinutes;
            var answerCount = interview.UserAnswers.Count;
            
            if (totalTimeMinutes <= 0 || answerCount == 0) return 0.0;

            var averageTimePerAnswer = totalTimeMinutes / answerCount;
            
            // Optimal time per answer is 2-5 minutes
            if (averageTimePerAnswer < 1) return 5.0; // Too rushed
            if (averageTimePerAnswer < 2) return 8.0;
            if (averageTimePerAnswer < 5) return 10.0; // Optimal
            if (averageTimePerAnswer < 8) return 7.0;
            if (averageTimePerAnswer < 15) return 5.0;
            return 3.0; // Too slow
        }

        public string GetScoreGrade(double score)
        {
            return score switch
            {
                >= 90 => "A+",
                >= 85 => "A",
                >= 80 => "A-",
                >= 75 => "B+",
                >= 70 => "B",
                >= 65 => "B-",
                >= 60 => "C+",
                >= 55 => "C",
                >= 50 => "C-",
                >= 45 => "D+",
                >= 40 => "D",
                >= 35 => "D-",
                _ => "F"
            };
        }

        public string GetScoreFeedback(double score)
        {
            return score switch
            {
                >= 90 => "Excellent performance! You demonstrated exceptional knowledge and communication skills.",
                >= 80 => "Great job! You showed strong understanding and good communication.",
                >= 70 => "Good performance. You have solid knowledge with room for improvement.",
                >= 60 => "Fair performance. Focus on improving your explanations and depth of knowledge.",
                >= 50 => "Below average. Consider reviewing the topics and practicing more.",
                >= 40 => "Needs improvement. Significant work needed on knowledge and communication.",
                _ => "Poor performance. Extensive review and practice recommended."
            };
        }
    }
} 