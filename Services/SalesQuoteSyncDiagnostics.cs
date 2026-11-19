using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using RepoDbApi.Contracts;

namespace RepoDbApi.Services;

public static class SalesQuoteSyncDiagnostics
{
    public static SalesQuoteSyncFailure Describe(Exception exception, string traceId)
    {
        SqlException? sql = null;
        var stage = "sync";
        var root = exception;
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SalesQuoteSyncStepException step) stage = step.Stage;
            if (current is SqlException databaseError) sql = databaseError;
            root = current;
        }
        var reason = sql is null ? "Unexpected processing error. Check the API console using this traceId."
            : ExplainSqlError(sql.Number, sql.Message);
        return new($"Sync failed at {stage}. {reason}", traceId, stage, sql?.Number,
            string.IsNullOrWhiteSpace(sql?.Procedure) ? null : sql.Procedure,
            sql?.LineNumber, root.GetType().Name);
    }

    // Never echo raw SQL messages: truncation errors may contain customer data,
    // and login/connection errors may contain user or server identifiers.
    public static string ExplainSqlError(int number, string message)
    {
        var name = Regex.Match(message, "'(?<name>[A-Za-z_][A-Za-z0-9_.\\[\\]]*)'").Groups["name"].Value;
        return number switch
        {
            208 => name.Length > 0 ? $"Required table/view '{name}' does not exist in the configured database." : "A required database table/view is missing.",
            207 => name.Length > 0 ? $"Required column '{name}' does not exist." : "A required database column is missing.",
            2812 => "The stored procedure is missing. Deploy sql/usp_UpdateSalesQuoteStagingMappings.sql in CargoMintDB.",
            51101 => "The deployed mapping procedure is outdated and rejects the API transaction. Redeploy sql/usp_UpdateSalesQuoteStagingMappings.sql.",
            51100 => "SalesQuoteID and OrgId are required by the mapping procedure.",
            51102 => "The mapping procedure could not acquire the sales quote lock. Retry shortly.",
            51103 => "The mapping procedure did not find exactly one staged quotation.",
            51104 => "OrgId does not exist in CargoMint Organizations.",
            229 => "The database login lacks permission for this operation.",
            18456 => "Database authentication failed. Verify the configured login.",
            4060 => "The configured database cannot be opened by this login.",
            -2 => "The database operation timed out.",
            1205 => "A database deadlock interrupted sync. Retry the request.",
            515 => "A required target column received NULL. Check the staging schema.",
            2628 or 8152 => "A source value exceeds the target column length. Check the staging schema.",
            2601 or 2627 => "A duplicate key was detected in staging.",
            547 => "A database constraint rejected the staged data.",
            _ => "A database operation failed. Check the API console using the SQL error number and traceId."
        };
    }
}
