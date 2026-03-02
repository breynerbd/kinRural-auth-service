using AuthService.Domain.Entitis;
namespace AuthService.Domain.Interfaces;

public interface IRoleRepository {
    Task<Role?> GetRoleByIdAsync(string id);
    Task<int?> CountUserInRoleAsync(string roleId);
    Task<IReadOnlyList<User>> GetUserByRoleAsync(string roleId);
    Task<IReadOnlyList<string>> GetUserRoleNameAsync(string userId);
    Task<Role> GetByNameAsync(string name);
}