using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoDocumentsService
{
    Task<IEnumerable<CargoDocuments>> GetByCargoIdAsync(int cargoId);
}
