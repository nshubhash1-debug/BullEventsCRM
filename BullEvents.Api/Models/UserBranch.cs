namespace BullEvents.Api.Models;

public class UserBranch
{
    public int UserId { get; set; }
    public int BranchId { get; set; }

    public User? User { get; set; }
    public Branch? Branch { get; set; }
}
