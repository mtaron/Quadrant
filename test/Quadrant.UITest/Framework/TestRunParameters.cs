using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Quadrant.UITest.Framework
{
    public sealed class TestRunParameters
    {
        private const string ParameterElementName = "Parameter";
        private const int DefaultIterations = 1;
        private readonly Dictionary<string, string> _parameters = new Dictionary<string, string>();

        private TestRunParameters(string settingsFilePath)
        {
            if (!File.Exists(settingsFilePath))
            {
                return;
            }

            using (var reader = XmlReader.Create(settingsFilePath))
            {
                reader.ReadToDescendant("TestRunParameters");
                reader.ReadToDescendant(ParameterElementName);
                do
                {
                    string name = reader.GetAttribute("name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        _parameters[name] = reader.GetAttribute("value");
                    }
                }
                while (reader.ReadToNextSibling(ParameterElementName));
            }

            if (_parameters.TryGetValue(nameof(Iterations), out string iterationsString)
                && !string.IsNullOrEmpty(iterationsString)
                && int.TryParse(iterationsString, out int iterations))
            {
                Iterations = iterations;
            }
            else
            {
                Iterations = DefaultIterations;
            }

            if (_parameters.TryGetValue(nameof(LogFolder), out string logFolder))
            {
                LogFolder = logFolder;
            }
        }

        public static TestRunParameters Read(string settingsFilePath = "UITest.runsettings")
        {
            return new TestRunParameters(settingsFilePath);
        }

        public int Iterations { get; }

        public string LogFolder { get; }
    }
}
