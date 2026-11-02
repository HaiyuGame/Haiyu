namespace ABIRuntime.Abstractions;

public enum PrivilegedStage
{
    Preparing,
    RequestingElevation,
    Connecting,
    Executing,
    Completed,
}
