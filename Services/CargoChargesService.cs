using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoChargesService : ICargoChargesService
{
    private readonly ICargoChargesRepository _repository;

    public CargoChargesService(ICargoChargesRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoCharges>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
