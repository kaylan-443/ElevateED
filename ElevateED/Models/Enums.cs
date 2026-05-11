namespace ElevateED.Models
{
    public enum UserRole
    {
        Applicant,
        Student,
        Parent,
        Teacher,
        Admin,
        Principal
    }

    public enum ApplicationStatus
    {
        Pending,
        UnderReview,
        Approved,
        Rejected
    }

    public enum Gender
    {
        Male,
        Female,
        Other
    }

    public enum SubmissionStatus
    {
        Submitted,   // Student has submitted
        Graded,      // Teacher has graded
        Returned,    // Graded and returned to student
        Late         // Submitted after due date
    }
}