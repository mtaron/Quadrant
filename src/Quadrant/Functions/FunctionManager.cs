using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FunctionInterpreter;
using Quadrant.Controls;
using Quadrant.Persistence;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.UI;

namespace Quadrant.Functions
{
    public class FunctionManager : NotifyingObject
    {
        public event EventHandler Invalidated;

        private static readonly Color[] FunctionColors = new Color[]
        {
            Color.FromArgb(255, 0, 104, 232),
            Color.FromArgb(255, 88, 173, 23),
            Color.FromArgb(255, 255, 208, 5),
            Color.FromArgb(255, 255, 141, 0),
            Color.FromArgb(255, 199, 39, 2)
        };

        private static readonly string[] KnownFunctions = new string[]
        {
            "abs",
            "acos",
            "asin",
            "atan",
            "ceiling",
            "cos",
            "cosh",
            "floor",
            "log",
            "log10",
            "round",
            "sin",
            "sinh",
            "sqrt",
            "tan",
            "tanh",
            "max",
            "min",
        };

        private AngleType _angleType;
        private CompileResult _result;

        public ObservableCollection<FunctionData> Functions { get; } = new ObservableCollection<FunctionData>();

        public IEnumerable<CompileError> Errors { get; private set; }

        public bool UseRadians
        {
            get => _angleType == AngleType.Radian;
            set
            {
                if (value)
                {
                    SetAngleType(AngleType.Radian);
                }
                else
                {
                    SetAngleType(AngleType.Degree);
                }

                OnPropertyChanged();
            }
        }

        public bool Compile()
        {
            QuadrantEventSource.Log.CompileStart();

            _result = Compiler.Compile(
                Functions.Select(f => string.Concat(f.Name, "=", f.Expression)),
                _angleType,
                CultureInfo.CurrentUICulture);
            bool hasError = !_result.IsSuccess;
            QuadrantEventSource.Log.CompileStop(hasError);

            if (hasError)
            {
                Errors = _result.Errors;
                return false;
            }

            for (int functionIndex = 0; functionIndex < Functions.Count; functionIndex++)
            {
                Functions[functionIndex].Function = _result.Functions[functionIndex];
            }

            Invalidate();
            return true;
        }

        internal void Serialize(Serializer serializer)
        {
            serializer.Write((byte)_angleType);
            serializer.Write(Functions.Count);
            foreach (FunctionData function in Functions)
            {
                function.Serialize(serializer);
            }
        }

        internal void Deserialize(Deserializer deserializer)
        {
            _angleType = (AngleType)deserializer.ReadByte();

            Functions.Clear();

            int count = deserializer.ReadInt32();
            for (int functionIndex = 0; functionIndex < count; functionIndex++)
            {
                FunctionData function = FunctionData.Deserialize(deserializer);
                Functions.Add(function);
            }

            Compile();
        }

        public IEnumerable<string> GetDependentFunctions(FunctionData function)
        {
            string functionName = function.Name;
            return _result.GetDependentFunctions(functionName).Append(functionName);
        }

        public IEnumerable<string> GetIndependentFunctions(FunctionData function)
        {
            IEnumerable<string> dependentFunctions = GetDependentFunctions(function);
            IEnumerable<string> independentFunctions = Functions.Select(f => f.Name).Except(dependentFunctions);
            return KnownFunctions.Concat(independentFunctions);
        }

        public FunctionData CreateFunction()
        {
            int id = GetNextFunctionId();
            string functionName = $"f{id}";

            FunctionData function = new FunctionData(functionName, GetColor(id), id);

            Functions.Insert(id - 1, function);

            AppTelemetry.Current.TrackEvent(
                TelemetryEvents.AddFunction,
                TelemetryProperties.Function,
                functionName);

            return function;
        }

        public void DeleteFunction(FunctionData function)
            => Functions.Remove(function);

        public async Task<IReadOnlyList<int>> DeleteFunctionAsync(FunctionData function, bool showPrompt = true)
        {
            IReadOnlyList<int> removedFunctions = await DeleteConfirmationDialog.DeleteFunctionAsync(function, this, showPrompt);
            if (removedFunctions.Any())
            {
                Invalidate();
            }

            return removedFunctions;
        }

        private int GetNextFunctionId()
        {
            int id = 1;
            foreach (var function in Functions)
            {
                if (id < function.Id)
                {
                    return id;
                }
                else
                {
                    id++;
                }
            }

            return id;
        }

        private void SetAngleType(AngleType angleType)
        {
            if (angleType == _angleType)
            {
                return;
            }

            _angleType = angleType;
            Compile();

            AppTelemetry.Current.TrackEvent(
                TelemetryEvents.ChangeAngleType,
                TelemetryProperties.AngleType,
                _angleType.ToString());
        }

        private void Invalidate()
            => Invalidated?.Invoke(this, EventArgs.Empty);

        private static Color GetColor(int id)
            => FunctionColors[(id - 1) % FunctionColors.Length];
    }
}
