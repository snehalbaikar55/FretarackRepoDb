using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class JobVendorDetailsService : IJobVendorDetailsService
{
    private readonly IJobVendorDetailsRepository _repository;

    public JobVendorDetailsService(IJobVendorDetailsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<JobVendorDetails>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
