using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// Vi phạm unique index/constraint của SQL Server (2601, 2627).
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
