namespace Vector.Core.Services;

public enum ApprovalStatus
{
    Approved,
    Denied,
    NeedsReview
}

public interface ITaskGovernor
{
    ApprovalStatus ValidateAction(string toolName, string input);
    void RecordAction(string toolName, string input);
    void Reset();
}
