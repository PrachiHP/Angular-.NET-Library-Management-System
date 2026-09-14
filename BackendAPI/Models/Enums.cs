namespace BackendAPI.Models;

public enum UserRole
{
    Librarian,
    Member
}

public enum RequestStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ProblemType
{
    TornPage,
    MissingPage,
    WaterDamage,
    BrokenSpine,
    Other
}
