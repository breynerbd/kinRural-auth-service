namespace AuthService.Domain.Constants;

public class RoleConstants {
    public const string ADMIN_ROL = "ADMIN_ROLE";
    public const string USER_ROL = "USER_ROLE";
    public static readonly string[] AllowedRoles = {ADMIN_ROL, USER_ROL};
}