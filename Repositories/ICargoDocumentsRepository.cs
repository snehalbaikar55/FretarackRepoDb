using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoDocumentsRepository
{
    Task<IEnumerable<CargoDocuments>> GetByCargoIdAsync(int cargoId);
    Task<CargoDocuments?> GetByIdAsync(int cargoDocumentId);
}
