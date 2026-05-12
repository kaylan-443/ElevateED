using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ElevateED.Services
{
    public class AIStudyService
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private static readonly HttpClient _httpClient;

        static AIStudyService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public AIStudyService()
        {
            _apiKey = ConfigurationManager.AppSettings["GeminiApiKey"] ?? "";
            _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
        }

        public async Task<string> GenerateFlashcards(string notesText)
        {
            var prompt = @"You are an expert educator creating study flashcards for high school students.

Convert these study notes into 8-10 clear, useful flashcards.

FORMAT EACH FLASHCARD EXACTLY LIKE THIS:
Q: [Clear, focused question]
A: [Concise, accurate answer]

RULES:
- Questions must test understanding, not just memorization
- Answers must be complete but brief (1-3 sentences max)
- Focus on the most important concepts
- Use simple language a student would understand
- Separate each card with ---

STUDY NOTES:
" + TruncateText(notesText, 8000);

            return await CallGeminiAsync(prompt);
        }

        public async Task<string> SimplifyNotes(string notesText)
        {
            var prompt = @"You are a friendly, patient teacher explaining concepts to a student who is struggling.

Break down these study notes into an easy-to-understand explanation.

FORMAT YOUR RESPONSE LIKE THIS:

🎯 MAIN IDEA
[Explain the core concept in 1-2 simple sentences]

📋 KEY POINTS
• Point 1 - with simple explanation
• Point 2 - with simple explanation  
• Point 3 - with simple explanation
(continue for all key points)

💡 REAL-WORLD EXAMPLE
[Give a practical example that makes it relatable]

🔑 THINGS TO REMEMBER
• Important point 1
• Important point 2
• Important point 3

RULES:
- Use VERY simple language (imagine explaining to a 14-year-old)
- Use analogies and comparisons
- Be encouraging and supportive
- Highlight the most important takeaways

STUDY NOTES:
" + TruncateText(notesText, 8000);

            return await CallGeminiAsync(prompt);
        }

        public async Task<string> GenerateExamNotes(string notesText)
        {
            var prompt = @"You are an expert exam preparation coach. Create well-structured revision notes that will help a student ace their exam.

FORMAT YOUR RESPONSE LIKE THIS:

━━━━━━━━━━━━━━━━━━━━━━━
📚 EXAM REVISION NOTES
━━━━━━━━━━━━━━━━━━━━━━━

## TOPIC OVERVIEW
[2-3 sentence summary of the topic]

## KEY TERMS & DEFINITIONS
**Term 1:** Clear definition
**Term 2:** Clear definition
**Term 3:** Clear definition

## IMPORTANT CONCEPTS
### Concept 1
• Main point
• Supporting detail
• Example

### Concept 2  
• Main point
• Supporting detail
• Example

## COMMON EXAM QUESTIONS
Q1: [Typical question]
→ Answer approach: [How to answer it]

Q2: [Typical question]  
→ Answer approach: [How to answer it]

## QUICK REVIEW (Read this before the exam!)
• Must-remember point 1
• Must-remember point 2
• Must-remember point 3

RULES:
- Use clear headings
- Bold key terms
- Include practical examples
- Make it scannable for last-minute review

STUDY NOTES:
" + TruncateText(notesText, 8000);

            return await CallGeminiAsync(prompt);
        }

        public async Task<string> GenerateQuiz(string notesText)
        {
            var prompt = @"You are an experienced teacher creating a multiple-choice quiz to test student understanding.

Generate 10 multiple-choice questions from these study notes.

FORMAT EACH QUESTION EXACTLY LIKE THIS:

Q: [Clear question that tests understanding]
A) [Plausible option]
B) [Plausible option]
C) [Plausible option]
D) [Plausible option]
Correct: [A/B/C/D]

RULES:
- Questions must test comprehension, not just recall
- All options must be plausible (not obviously wrong)
- Only ONE correct answer per question
- Vary the difficulty (some easy, some challenging)
- Cover different topics from the notes
- Separate each question with ---

STUDY NOTES:
" + TruncateText(notesText, 8000);

            return await CallGeminiAsync(prompt);
        }

        private async Task<string> CallGeminiAsync(string prompt)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return GenerateFallback(prompt);
            }

            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 4096,
                        topP = 0.95
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = _baseUrl + "?key=" + _apiKey;

                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic aiResponse = JsonConvert.DeserializeObject<dynamic>(responseText);
                    if (aiResponse.candidates != null && aiResponse.candidates.Count > 0)
                    {
                        return (string)aiResponse.candidates[0].content.parts[0].text;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Gemini Error: " + ex.Message);
            }

            return GenerateFallback(prompt);
        }

        private string GenerateFallback(string prompt)
        {
            if (prompt.Contains("flashcard"))
            {
                return @"Q: What is the main topic covered in these notes?
A: The notes cover key concepts that form the foundation of this subject. Review the main headings to identify the core topic.

---
Q: Why is understanding this topic important?
A: This topic is essential because it builds the foundation for more advanced concepts. Mastering it now will make future learning much easier.

---
Q: What are the key terms you should remember?
A: Focus on the bold and highlighted terms in your notes. Create a glossary and review it regularly.

---
Q: How can you apply this knowledge in practice?
A: Apply these concepts by solving practice questions, discussing with classmates, and looking for real-world examples that demonstrate these principles.

---
Q: What's the best way to study this material?
A: Break the material into smaller chunks, use active recall (test yourself), and review regularly over time rather than cramming.";
            }

            if (prompt.Contains("simple language") || prompt.Contains("struggling"))
            {
                return @"🎯 MAIN IDEA
The core concept here is understanding the fundamental principles and how they connect to each other. Think of it like building a house - you need a strong foundation first.

📋 KEY POINTS
• Break complex ideas into smaller, manageable pieces
• Connect new information to things you already know
• Practice applying concepts, not just memorizing facts
• Use visual aids like diagrams and mind maps to understand relationships

💡 REAL-WORLD EXAMPLE
Imagine learning to cook. You don't start by making a complex 5-course meal. You start with basic techniques - boiling water, chopping vegetables, understanding heat levels. Once you master these basics, you can combine them to create amazing dishes. Learning this subject works the same way!

🔑 THINGS TO REMEMBER
• Everyone learns at their own pace - don't compare yourself to others
• Making mistakes is part of the learning process
• Ask questions when you're confused
• Review material within 24 hours to improve retention";
            }

            if (prompt.Contains("exam"))
            {
                return @"━━━━━━━━━━━━━━━━━━━━━━━
📚 EXAM REVISION NOTES
━━━━━━━━━━━━━━━━━━━━━━━

## TOPIC OVERVIEW
This topic covers essential concepts that frequently appear in exams. Master these fundamentals to build confidence for more advanced material.

## KEY TERMS & DEFINITIONS
**Core Concept:** The fundamental principle underlying the topic
**Application:** How the concept is used in real scenarios
**Methodology:** The step-by-step approach to solving related problems

## IMPORTANT CONCEPTS
### Foundation Principles
• Understand the basic building blocks
• Know how different concepts connect
• Practice identifying patterns

### Practical Applications
• Apply theory to practice questions
• Work through example problems
• Check your answers and learn from mistakes

## COMMON EXAM QUESTIONS
Q1: Explain the relationship between key concepts
→ Start by defining each concept, then explain how they connect. Use examples to support your answer.

Q2: Apply the theory to a given scenario
→ Read the scenario carefully, identify which concepts apply, and explain your reasoning clearly.

## QUICK REVIEW (Read this before the exam!)
• Stay calm and read questions carefully
• Show your working for calculation questions
• Manage your time - don't spend too long on one question
• Review your answers if you have time at the end";
            }

            // Quiz fallback
            return @"Q: What is the most important factor for effective studying?
A) Cramming the night before
B) Regular review and practice
C) Reading notes once
D) Highlighting everything
Correct: B

---
Q: Which study technique is proven most effective?
A) Passive reading
B) Active recall
C) Copying notes
D) Listening without notes
Correct: B

---
Q: What helps with long-term retention?
A) Studying for 8 hours straight
B) Spaced repetition over time
C) Studying only before exams
D) Skipping difficult topics
Correct: B

---
Q: How should you approach difficult concepts?
A) Skip them entirely
B) Break them into smaller parts
C) Memorize without understanding
D) Only read about them once
Correct: B

---
Q: What's the best way to prepare for an exam?
A) Read notes once the night before
B) Practice with past questions and review weak areas
C) Only focus on easy topics
D) Study without breaks
Correct: B";
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}