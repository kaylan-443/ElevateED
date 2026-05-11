using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity.ModelConfiguration;

namespace ElevateED.Models
{
    public class QuizQuestionConfiguration : EntityTypeConfiguration<QuizQuestion>
    {
        public QuizQuestionConfiguration()
        {
            // Disable cascade delete on ALL foreign keys
            HasRequired(q => q.Subject)
                .WithMany()
                .HasForeignKey(q => q.SubjectId)
                .WillCascadeOnDelete(false);

            HasRequired(q => q.Grade)
                .WithMany()
                .HasForeignKey(q => q.GradeId)
                .WillCascadeOnDelete(false);

            HasRequired(q => q.Teacher)
                .WithMany()
                .HasForeignKey(q => q.CreatedBy)
                .WillCascadeOnDelete(false);
        }
    }

    public class QuizAttemptConfiguration : EntityTypeConfiguration<QuizAttempt>
    {
        public QuizAttemptConfiguration()
        {
            HasRequired(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .WillCascadeOnDelete(false);

            HasRequired(a => a.Subject)
                .WithMany()
                .HasForeignKey(a => a.SubjectId)
                .WillCascadeOnDelete(false);
        }
    }

    public class QuizAnswerConfiguration : EntityTypeConfiguration<QuizAnswer>
    {
        public QuizAnswerConfiguration()
        {
            HasRequired(a => a.QuizAttempt)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuizAttemptId)
                .WillCascadeOnDelete(true);

            HasRequired(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .WillCascadeOnDelete(false);
        }
    }
}