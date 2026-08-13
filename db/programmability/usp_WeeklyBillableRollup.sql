/*
    dbo.usp_WeeklyBillableRollup
    ----------------------------
    Per ISO week and client: billable and non-billable hours, the amount billed, the client's
    running billable total, the change against the prior week, and the client's standing that
    week.

    Contract: specs/003-weekly-billable-rollup/contracts/usp-weekly-billable-rollup.md

    Applied by the bootstrap script's apply-procedures step, never by an EF migration
    (constitution P7). ProcedureApplier executes this file as a single SqlCommand, which has
    no batch parser -- so there is no GO anywhere in this file, and nothing may precede the
    CREATE OR ALTER, which must be first in its batch. SET NOCOUNT ON goes inside the body,
    where it is legal.
*/
CREATE OR ALTER PROCEDURE dbo.usp_WeeklyBillableRollup
    @FromDate date,
    @ToDate   date,
    @ClientId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /*
        Result-set contract, fixed before anything binds to it.

        The aggregation lands in the following tasks; until then this returns the exact twelve
        columns with their final types and no rows, so a caller that maps them wrongly fails on
        the mapping rather than on a missing object. Every CAST here is load-bearing: without
        them SQL Server infers the type from the literal and the reader binds to the wrong one.
    */
    SELECT
        IsoYear                 = CAST(0 AS int),
        IsoWeek                 = CAST(0 AS int),
        WeekStartDate           = CAST('19000101' AS date),
        ClientId                = CAST(0 AS int),
        ClientCode              = CAST('' AS nvarchar(20)),
        ClientName              = CAST('' AS nvarchar(200)),
        BillableHours           = CAST(0 AS decimal(12, 2)),
        NonBillableHours        = CAST(0 AS decimal(12, 2)),
        BillableAmount          = CAST(0 AS decimal(14, 2)),
        CumulativeBillableHours = CAST(0 AS decimal(12, 2)),
        HoursDeltaVsPriorWeek   = CAST(NULL AS decimal(12, 2)),
        ClientRankInWeek        = CAST(0 AS int)
    WHERE 1 = 0;
END
