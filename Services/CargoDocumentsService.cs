using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoDocumentsService : ICargoDocumentsService
{
    private readonly ICargoDocumentsRepository _repository;

    public CargoDocumentsService(ICargoDocumentsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoDocuments>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }

    public async Task<CargoDocuments?> GetByIdAsync(int cargoDocumentId)
    {
        return await _repository.GetByIdAsync(cargoDocumentId);
    }
}
