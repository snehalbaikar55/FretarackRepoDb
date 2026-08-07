using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RepoDbApi.Contracts;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/connection-test")]
public class ConnectionTestController : ControllerBase
{
    private readonly string _sourceConnectionString;
    private readonly string _targetConnectionString;
    private readonly ILogger<ConnectionTestController> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public ConnectionTestController(
        IConfiguration configuration,
        ILogger<ConnectionTestController> logger,
        IHostEnvironment hostEnvironment)
    {
        _sourceConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        _targetConnectionString = configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet("source")]
    public Task<IActionResult> TestSourceConnection(CancellationToken cancellationToken)
    {
        return TestConnectionAsync("sourceConnection", _sourceConnectionString, cancellationToken);
    }

    [HttpGet("target")]
    public Task<IActionResult> TestTargetConnection(CancellationToken cancellationToken)
    {
        return TestConnectionAsync("targetConnection", _targetConnectionString, cancellationToken);
    }

    private async Task<IActionResult> TestConnectionAsync(
        string connectionName,
        string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            var builder = new SqlConnectionStringBuilder(connectionString);
            return Ok(new ConnectionTestResponse
            {
                ConnectionName = connectionName,
                IsConnected = true,
                Message = "Connection successful.",
                Database = builder.InitialCatalog,
                DataSource = builder.DataSource
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect using {ConnectionName}.", connectionName);

            var response = new ConnectionTestResponse
            {
                ConnectionName = connectionName,
                IsConnected = false,
                Message = _hostEnvironment.IsDevelopment()
                    ? ex.Message
                    : "Connection failed."
            };

            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }
}
