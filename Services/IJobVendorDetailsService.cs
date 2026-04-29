using GeneratedModels;

namespace RepoDbApi.Services;

public interface IJobVendorDetailsService
{
    Task<IEnumerable<JobVendorDetails>> GetByCargoIdAsync(int cargoId);
}
