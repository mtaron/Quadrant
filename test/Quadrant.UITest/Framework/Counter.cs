namespace Quadrant.UITest.Framework
{
    public class Counter
    {
        public Counter(string name, int count)
        {
            Name = name;
            Count = count;
        }

        public string Name { get; }

        public int Count { get; }
    }
}
