namespace BullEvents.Api.Models;

public class User
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.SalesAgent;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Blocks the CRM until the user picks their own password.
    ///
    /// Set on every account created for somebody else — an invite, a bulk
    /// import — because those all start life sharing a password the person who
    /// created them knows. Until it is cleared the session can do exactly one
    /// thing: change the password.
    /// </summary>
    public bool MustChangePassword { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>
    /// When this account last completed a sign-in, second factor included.
    ///
    /// Stamped after the OTP rather than after the password, so a stolen
    /// password that never got past verification does not read as a login. Null
    /// means the account has never been used — the state an administrator
    /// actually wants to see, and the reason this is nullable rather than
    /// defaulted to the creation time.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /* ---------------- hierarchy ---------------- */

    /// <summary>
    /// Who this user reports to.
    ///
    /// The reporting line is what gives a team-scoped role its team: an AGM
    /// sees themselves plus everyone below them, to any depth, rather than a
    /// list somebody has to maintain by hand. Null means they report to nobody
    /// — the top of a line, not an orphan.
    /// </summary>
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public ICollection<User> Reports { get; set; } = new List<User>();

    /* ---------------- who they are on the org chart ---------------- */

    /// <summary>
    /// Their job title, from the company's own list.
    ///
    /// Separate from <see cref="Role"/>, which is what the software lets them
    /// do. Two people can share the title "Manager" and see entirely different
    /// things, and a company adds a title without anybody touching permissions.
    /// </summary>
    public int? DesignationId { get; set; }
    public Designation? Designation { get; set; }

    /// <summary>Employee code, as HR writes it. Shown on the org chart and in exports.</summary>
    public string? EmployeeCode { get; set; }

    public DateTime? JoinedOn { get; set; }

    public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();

    /* ---------------- module access ---------------- */

    /// <summary>
    /// Modules turned on or off for this person specifically, over and above
    /// what their role grants. Empty for almost everyone — the role is meant to
    /// be the answer, and an override is the exception a company argues for.
    /// </summary>
    public ICollection<UserModuleGrant> ModuleGrants { get; set; } = new List<UserModuleGrant>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
}
