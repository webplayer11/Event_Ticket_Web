using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventGO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.ReserveInventory
                    @InventoryId uniqueidentifier,
                    @Quantity int
                AS
                BEGIN
                    SET NOCOUNT ON;

                    UPDATE dbo.TicketTypes WITH (UPDLOCK, ROWLOCK)
                    SET ReservedQuantity = ReservedQuantity + @Quantity
                    WHERE Id = @InventoryId
                      AND @Quantity > 0
                      AND TotalQuantity - ReservedQuantity - SoldQuantity >= @Quantity;

                    DECLARE @AffectedRows int = @@ROWCOUNT;
                    SELECT CAST(CASE WHEN @AffectedRows = 1 THEN 1 ELSE 0 END AS int) AS ResultCode;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.ExpireReservation
                    @ReservationId uniqueidentifier,
                    @Now datetimeoffset
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    BEGIN TRANSACTION;

                    DECLARE @Status int;
                    DECLARE @ExpiresAt datetimeoffset;

                    SELECT
                        @Status = Status,
                        @ExpiresAt = ExpiresAt
                    FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE Id = @ReservationId;

                    IF @Status = 2
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(2 AS int) AS ResultCode;
                        RETURN;
                    END;

                    IF @Status IS NULL OR @Status <> 0 OR @ExpiresAt > @Now
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(0 AS int) AS ResultCode;
                        RETURN;
                    END;

                    DECLARE @ItemCount int;
                    DECLARE @ValidInventoryCount int;

                    SELECT @ItemCount = COUNT(*)
                    FROM dbo.ReservationItems
                    WHERE ReservationId = @ReservationId;

                    SELECT @ValidInventoryCount = COUNT(*)
                    FROM dbo.ReservationItems AS item
                    INNER JOIN dbo.TicketTypes AS inventory WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        ON inventory.Id = item.TicketTypeId
                    WHERE item.ReservationId = @ReservationId
                      AND inventory.ReservedQuantity >= item.Quantity;

                    IF @ItemCount = 0 OR @ValidInventoryCount <> @ItemCount
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(4 AS int) AS ResultCode;
                        RETURN;
                    END;

                    UPDATE inventory
                    SET ReservedQuantity = inventory.ReservedQuantity - item.Quantity
                    FROM dbo.TicketTypes AS inventory
                    INNER JOIN dbo.ReservationItems AS item
                        ON item.TicketTypeId = inventory.Id
                    WHERE item.ReservationId = @ReservationId;

                    UPDATE dbo.Reservations
                    SET Status = 2,
                        ClosedAt = @Now
                    WHERE Id = @ReservationId
                      AND Status = 0;

                    COMMIT TRANSACTION;
                    SELECT CAST(1 AS int) AS ResultCode;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.CancelReservation
                    @ReservationId uniqueidentifier,
                    @Now datetimeoffset
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    BEGIN TRANSACTION;

                    DECLARE @Status int;

                    SELECT @Status = Status
                    FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE Id = @ReservationId;

                    IF @Status = 3
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(2 AS int) AS ResultCode;
                        RETURN;
                    END;

                    IF @Status IS NULL OR @Status <> 0
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(0 AS int) AS ResultCode;
                        RETURN;
                    END;

                    DECLARE @ItemCount int;
                    DECLARE @ValidInventoryCount int;

                    SELECT @ItemCount = COUNT(*)
                    FROM dbo.ReservationItems
                    WHERE ReservationId = @ReservationId;

                    SELECT @ValidInventoryCount = COUNT(*)
                    FROM dbo.ReservationItems AS item
                    INNER JOIN dbo.TicketTypes AS inventory WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        ON inventory.Id = item.TicketTypeId
                    WHERE item.ReservationId = @ReservationId
                      AND inventory.ReservedQuantity >= item.Quantity;

                    IF @ItemCount = 0 OR @ValidInventoryCount <> @ItemCount
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(4 AS int) AS ResultCode;
                        RETURN;
                    END;

                    UPDATE inventory
                    SET ReservedQuantity = inventory.ReservedQuantity - item.Quantity
                    FROM dbo.TicketTypes AS inventory
                    INNER JOIN dbo.ReservationItems AS item
                        ON item.TicketTypeId = inventory.Id
                    WHERE item.ReservationId = @ReservationId;

                    UPDATE dbo.Reservations
                    SET Status = 3,
                        ClosedAt = @Now
                    WHERE Id = @ReservationId
                      AND Status = 0;

                    COMMIT TRANSACTION;
                    SELECT CAST(1 AS int) AS ResultCode;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.ConvertReservation
                    @ReservationId uniqueidentifier,
                    @Now datetimeoffset
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    BEGIN TRANSACTION;

                    DECLARE @Status int;
                    DECLARE @ExpiresAt datetimeoffset;

                    SELECT
                        @Status = Status,
                        @ExpiresAt = ExpiresAt
                    FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE Id = @ReservationId;

                    IF @Status = 1
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(2 AS int) AS ResultCode;
                        RETURN;
                    END;

                    IF @Status IS NULL OR @Status <> 0
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(0 AS int) AS ResultCode;
                        RETURN;
                    END;

                    IF @ExpiresAt <= @Now
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(3 AS int) AS ResultCode;
                        RETURN;
                    END;

                    DECLARE @ItemCount int;
                    DECLARE @ValidInventoryCount int;

                    SELECT @ItemCount = COUNT(*)
                    FROM dbo.ReservationItems
                    WHERE ReservationId = @ReservationId;

                    SELECT @ValidInventoryCount = COUNT(*)
                    FROM dbo.ReservationItems AS item
                    INNER JOIN dbo.TicketTypes AS inventory WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        ON inventory.Id = item.TicketTypeId
                    WHERE item.ReservationId = @ReservationId
                      AND inventory.ReservedQuantity >= item.Quantity
                      AND inventory.ReservedQuantity + inventory.SoldQuantity <= inventory.TotalQuantity;

                    IF @ItemCount = 0 OR @ValidInventoryCount <> @ItemCount
                    BEGIN
                        COMMIT TRANSACTION;
                        SELECT CAST(4 AS int) AS ResultCode;
                        RETURN;
                    END;

                    UPDATE inventory
                    SET ReservedQuantity = inventory.ReservedQuantity - item.Quantity,
                        SoldQuantity = inventory.SoldQuantity + item.Quantity
                    FROM dbo.TicketTypes AS inventory
                    INNER JOIN dbo.ReservationItems AS item
                        ON item.TicketTypeId = inventory.Id
                    WHERE item.ReservationId = @ReservationId;

                    UPDATE dbo.Reservations
                    SET Status = 1,
                        ClosedAt = @Now
                    WHERE Id = @ReservationId
                      AND Status = 0;

                    COMMIT TRANSACTION;
                    SELECT CAST(1 AS int) AS ResultCode;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ConvertReservation;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.CancelReservation;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ExpireReservation;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ReserveInventory;");
        }
    }
}
