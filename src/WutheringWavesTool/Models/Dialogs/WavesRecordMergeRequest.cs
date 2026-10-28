namespace Haiyu.Models.Dialogs;

public sealed record WavesRecordMergeRequest(string SessionId, string RecordName, nint WindowHandle);
