using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;

namespace Haiyu.Models.Dialogs;

public sealed record UpdateGameDialogRequest(string ContextName, UpdateGameType Operation, GameResourceParameter? Parameter);
public sealed record GameFolderDialogRequest(Type ContextType, GameResourceParameter? Parameter);

public sealed record SelectGameBunleRequest(string ContextName);
