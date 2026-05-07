using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace RepoDbApi.Repositories;

public class CargoDocumentsRepository : ICargoDocumentsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoDocumentsRepository> _logger;
    private readonly string? _ftpServerHost;
    private readonly string? _ftpUserName;
    private readonly string? _ftpPassword;

    public CargoDocumentsRepository(IConfiguration configuration, ILogger<CargoDocumentsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
        _ftpServerHost = configuration["Ftp:Server"];
        _ftpUserName = configuration["Ftp:UserName"];
        _ftpPassword = configuration["Ftp:Password"];

        _logger.LogInformation("FTP Username In Controller: {Username}", _ftpUserName);

        _logger.LogInformation(
        "Password Empty In Controller: {IsEmpty}",
        string.IsNullOrWhiteSpace(_ftpPassword) ? "Yes" : "No");
    }

    public async Task<IEnumerable<CargoDocuments>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoDocuments>(
                @"SELECT cd.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [CargoDocuments] cd
                  LEFT JOIN [UserMaster] um ON um.[UserID] = cd.[CreatedBy]
                  WHERE cd.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoDocuments records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }

  public async Task<CargoDocuments?> GetByIdAsync(int cargoDocumentId)
{
    try
    {
        await using var connection = new SqlConnection(_connectionString);

        var result = await connection.ExecuteQueryAsync<CargoDocuments>(
            @"SELECT cd.*,
                     COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
              FROM [CargoDocuments] cd
              LEFT JOIN [UserMaster] um
                     ON um.[UserID] = cd.[CreatedBy]
              WHERE cd.[CargoDocumentID] = @id",
            new { id = cargoDocumentId });

        var cargoDocument = result.FirstOrDefault();

        if (cargoDocument != null &&
            !string.IsNullOrWhiteSpace(cargoDocument.FTPLink))
        {
            string adjustedFTPLink = cargoDocument.FTPLink;

            // Normalize slashes
            adjustedFTPLink = adjustedFTPLink.Replace("\\", "/");

            // Replace FTP host dynamically
            if (!string.IsNullOrWhiteSpace(_ftpServerHost))
            {
                string ftpServerHostUpper = _ftpServerHost.ToUpperInvariant();
                string ftpLinkUpper = adjustedFTPLink.ToUpperInvariant();

                if (ftpServerHostUpper.Contains("SETUP.FRETLOG.COM") &&
                    ftpLinkUpper.Contains("192.168.0.10"))
                {
                    adjustedFTPLink = adjustedFTPLink.Replace(
                        "192.168.0.10",
                        "98.70.28.62",
                        StringComparison.OrdinalIgnoreCase);
                }
                else if (ftpServerHostUpper.Contains("192.168.0.10") &&
                         ftpLinkUpper.Contains("SETUP.FRETLOG.COM"))
                {
                    adjustedFTPLink = adjustedFTPLink.Replace(
                        "SETUP.FRETLOG.COM",
                        "98.70.28.62",
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            // Fix duplicate slashes after ftp://
            if (adjustedFTPLink.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                adjustedFTPLink =
                    "ftp://" +
                    adjustedFTPLink.Substring(6).Replace("//", "/");
            }

            cargoDocument.FTPLink = adjustedFTPLink;
        }

        return cargoDocument;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error while retrieving CargoDocument for CargoDocumentID: {CargoDocumentId}",
            cargoDocumentId);

        throw;
    }
}
}
