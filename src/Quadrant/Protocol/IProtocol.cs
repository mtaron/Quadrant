using System.Collections.Generic;

namespace Quadrant.Protocol
{
    public interface IProtocol
    {
        void ToggleAngleType();

        void SetTelemetryMode(bool isEnabled);

        int AddFunction(string function, out IReadOnlyList<string> errors);

        IReadOnlyList<int> RemoveFunction(int id);
    }
}
