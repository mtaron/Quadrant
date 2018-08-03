using System;
using System.Collections.Generic;
using Quadrant.Utility;
using Windows.Foundation.Collections;

namespace Quadrant.Protocol
{
    internal class ProtocolHandler
    {
        private readonly IProtocol _protocol;

        private const string ErrorKey = "Error";

        public ProtocolHandler(IProtocol protocol)
            => _protocol = protocol;

        public ValueSet Handle(ValueSet data)
        {
            var results = new ValueSet();
            if (data.Count == 0)
            {
                return results;
            }

            var command = data.GetValueOrDefault<string>("Command");
            if (string.IsNullOrEmpty(command))
            {
                results[ErrorKey] = "Command value not set";
                return results;
            }

            if (command.Equals("SetTelemetryMode", StringComparison.OrdinalIgnoreCase))
            {
                _protocol.SetTelemetryMode(data.GetValueOrDefault<bool>("IsEnabled"));
                return results;
            }

            if (command.Equals("AddFunction", StringComparison.OrdinalIgnoreCase))
            {
                var function = data.GetValueOrDefault<string>("Function");
                int id = _protocol.AddFunction(function, out IReadOnlyList<string> errors);
                results["Id"] = id;
                if (errors != null && errors.Count > 0)
                {
                    results[ErrorKey] = errors;
                }

                return results;
            }

            if (command.Equals("RemoveFunction", StringComparison.OrdinalIgnoreCase))
            {
                var id = data.GetValueOrDefault<int>("Id");
                IReadOnlyList<int> removedFunctions = _protocol.RemoveFunction(id);
                results["RemovedFunctions"] = removedFunctions;
                return results;
            }

            if (command.Equals("ToggleAngleType", StringComparison.OrdinalIgnoreCase))
            {
                _protocol.ToggleAngleType();
                return results;
            }

            results[ErrorKey] = "Unknown command";
            return results;
        }
    }
}
