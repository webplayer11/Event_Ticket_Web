using System.Data;
using System.Data.Common;
using EventGO.Application.Reservations;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventGO.Infrastructure.Reservations;

public sealed class SqlServerReservationDatabaseOperations
    : IReservationDatabaseOperations
{
    private readonly EventGoDbContext _dbContext;

    public SqlServerReservationDatabaseOperations(EventGoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryReservationResult> ReserveInventoryAsync(
        Guid ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteProcedureAsync(
            "dbo.ReserveInventory",
            command =>
            {
                AddParameter(command, "@InventoryId", DbType.Guid, ticketTypeId);
                AddParameter(command, "@Quantity", DbType.Int32, quantity);
            },
            cancellationToken);

        return result == (int)InventoryReservationResult.Success
            ? InventoryReservationResult.Success
            : InventoryReservationResult.InsufficientInventory;
    }

    public Task<ReservationTransitionResult> ExpireReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(
            "dbo.ExpireReservation",
            reservationId,
            now,
            cancellationToken);

    public Task<ReservationTransitionResult> CancelReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(
            "dbo.CancelReservation",
            reservationId,
            now,
            cancellationToken);

    public Task<ReservationTransitionResult> ConvertReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(
            "dbo.ConvertReservation",
            reservationId,
            now,
            cancellationToken);

    public async Task<bool> LockActiveReservationForOrderAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT COUNT_BIG(1)
            FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE Id = @ReservationId
              AND Status = 0
              AND ExpiresAt > @Now;
            """;

        var result = await ExecuteScalarAsync(
            sql,
            CommandType.Text,
            command =>
            {
                AddParameter(command, "@ReservationId", DbType.Guid, reservationId);
                AddParameter(command, "@Now", DbType.DateTimeOffset, now);
            },
            cancellationToken);

        return Convert.ToInt64(result) == 1;
    }

    private async Task<ReservationTransitionResult> ExecuteTransitionAsync(
        string procedureName,
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteProcedureAsync(
            procedureName,
            command =>
            {
                AddParameter(command, "@ReservationId", DbType.Guid, reservationId);
                AddParameter(command, "@Now", DbType.DateTimeOffset, now);
            },
            cancellationToken);

        return Enum.IsDefined(typeof(ReservationTransitionResult), result)
            ? (ReservationTransitionResult)result
            : ReservationTransitionResult.InventoryInvariantViolation;
    }

    private async Task<int> ExecuteProcedureAsync(
        string procedureName,
        Action<DbCommand> configure,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(
            procedureName,
            CommandType.StoredProcedure,
            configure,
            cancellationToken);

        return Convert.ToInt32(result);
    }

    private async Task<object> ExecuteScalarAsync(
        string commandText,
        CommandType commandType,
        Action<DbCommand> configure,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = commandType;
            command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
            configure(command);

            return await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Database operation {commandText} returned no result.");
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
