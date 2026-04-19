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
}