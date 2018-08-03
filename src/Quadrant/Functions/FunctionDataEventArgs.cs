using System;

namespace Quadrant.Functions
{
    public class FunctionDataEventArgs : EventArgs
    {
        public FunctionDataEventArgs(FunctionData function)
        {
            Function = function;
        }

        public FunctionData Function { get; }
    }
}
