using System;
using Vector.Core.Models;

namespace Vector.Core.Services;

public interface ISelfStateService
{
    SelfState GetState();
    void UpdateState(Action<SelfState> updateAction);
    void SaveState();
    void LoadState();
}
