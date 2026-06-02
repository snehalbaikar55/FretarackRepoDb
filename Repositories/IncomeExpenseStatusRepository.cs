using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class IncomeExpenseStatusRepository : IIncomeExpenseStatusRepository
{
    private readonly string _connectionString;
    private readonly ILogger<IncomeExpenseStatusRepository> _logger;

    public IncomeExpenseStatusRepository(IConfiguration configuration, ILogger<IncomeExpenseStatusRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<IncomeExpenseStatus>> GetByJobNoAsync(string jobNo)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            const string sql = "exec usp_IncomeExpenseStatus @JobNo";
            return await connection.ExecuteQueryAsync<IncomeExpenseStatus>(sql, new { JobNo = jobNo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving IncomeExpenseStatus for JobNo {JobNo}.", jobNo);
            throw;
        }
    }
}
