using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IJobVendorDetailsRepository
{
    Task<IEnumerable<JobVendorDetails>> GetByCargoIdAsync(int cargoId);
}
