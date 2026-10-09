namespace TuitionService.Application.Dtos;

public sealed record FeeInfo(long FeeId, string Semester, decimal Amount, string Status);

public sealed record TuitionLookupResult(string Mssv, string StudentName, FeeInfo Fee);
