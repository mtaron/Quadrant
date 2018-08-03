using System;
using MathNet.Numerics;
using Quadrant.Ink;
using Quadrant.Persistence;
using Quadrant.Utility;
using Windows.UI;

namespace Quadrant.Functions
{
    public sealed class FunctionData : NotifyingObject, IFunction
    {
        private Func<double, double> _function;
        private Func<double, double> _derivative;
        private string _expression;
        private Color _color;
        private string _displayExpression;
        private TransformedStrokes _strokes;

        public FunctionData(string name, Color color, int id)
        {
            Name = name;
            _color = color;
            Id = id;
        }

        public string Name { get; }

        public string Expression
        {
            get => _expression;
            set
            {
                if (_expression != value)
                {
                    _expression = value;
                    _displayExpression = null;
                    if (!string.IsNullOrEmpty(_expression))
                    {
                        _expression = _expression.Trim();
                        _expression = _expression.ToLower();
                    }
                    OnPropertyChanged(nameof(DisplayExpression));
                }
            }
        }

        public string DisplayExpression
        {
            get
            {
                if (_displayExpression == null)
                {
                    _displayExpression = GetDisplayExpression(Expression);
                }

                return _displayExpression;
            }
        }


        internal TransformedStrokes Strokes
        {
            get
            {
                if (_strokes == null)
                {
                    _strokes = new TransformedStrokes(allowMultipleStrokes: false);
                }

                return _strokes;
            }
        }

        public Func<double, double> Function
        {
            get => _function;

            internal set
            {
                if (_function != value)
                {
                    _function = value;
                    _derivative = null;
                };
            }
        }

        public Func<double, double> Derivative
        {
            get
            {
                if (_derivative == null)
                {
                    _derivative = Differentiate.FirstDerivativeFunc(_function);
                }

                return _derivative;
            }
        }

        public Color Color
        {
            get => _color;

            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Id { get; }

        internal void Serialize(Serializer serializer)
        {
            serializer.Write(Name);
            serializer.Write(Id);
            serializer.Write(Color);
            serializer.Write(Expression ?? string.Empty);
        }

        internal static FunctionData Deserialize(Deserializer deserializer)
        {
            string name = deserializer.ReadString();
            int id = deserializer.ReadInt32();
            Color color = deserializer.ReadColor();
            string expression = deserializer.ReadString();
            FunctionData function = new FunctionData(name, color, id)
            {
                Expression = expression
            };
            return function;
        }

        public override string ToString()
        {
            string displayExpression = GetDisplayExpression(Expression);
            return $"\u0192{Id}(x)={displayExpression}";
        }

        private static string GetDisplayExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return string.Empty;
            }

            string displayExpression = expression.Replace("*", "\u22C5");
            displayExpression = displayExpression.Replace("pi", "\u03C0");
            return displayExpression;
        }
    }
}
