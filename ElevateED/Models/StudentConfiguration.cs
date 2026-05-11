using System.Data.Entity.ModelConfiguration;
using System.Data.Entity.Infrastructure.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class StudentConfiguration : EntityTypeConfiguration<Student>
    {
        public StudentConfiguration()
        {
            // Disable cascade delete for UserId foreign key
            HasRequired(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .WillCascadeOnDelete(false);

            // Disable cascade delete for ApplicantId foreign key
            HasRequired(s => s.Applicant)
                .WithMany()
                .HasForeignKey(s => s.ApplicantId)
                .WillCascadeOnDelete(false);
        }
    }
}