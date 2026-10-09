namespace TuitionService.Domain.Entities;

/// <summary>
/// A student who owns one or more tuition fees. <c>mssv</c> is the student code used to look
/// up tuition and is unique within the tuition database.
/// </summary>
public sealed class Student
{
    private readonly List<TuitionFee> _tuitionFees = new();

    private Student()
    {
    }

    public Student(string mssv, string fullName, string? className, string? faculty, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(mssv))
        {
            throw new ArgumentException("MSSV is required.", nameof(mssv));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name is required.", nameof(fullName));
        }

        Mssv = mssv;
        FullName = fullName;
        ClassName = className;
        Faculty = faculty;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Reconstitution constructor for persistence and test fixtures.</summary>
    internal Student(long id, string mssv, string fullName, string? className, string? faculty, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Mssv = mssv;
        FullName = fullName;
        ClassName = className;
        Faculty = faculty;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    internal void AddFee(TuitionFee fee) => _tuitionFees.Add(fee);

    public long Id { get; private set; }
    public string Mssv { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? ClassName { get; private set; }
    public string? Faculty { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<TuitionFee> TuitionFees => _tuitionFees;
}
