namespace Quadrant.Utility
{
    public static class StringExtensions
    {
        /// <summary>
        /// Gets the start index and length of a smallest letter-only substring of
        /// <paramref name="text"/> containing <paramref name="location"/>.
        /// </summary>
        public static void GetLetterExtent(this string text, int location, out int start, out int length)
        {
            start = location;
            int end = location;
            if (char.IsLetter(text, start))
            {
                while (start > 0 && char.IsLetter(text, start - 1))
                {
                    start--;
                }

                while (end < text.Length && char.IsLetter(text, end))
                {
                    end++;
                }
            }

            length = end - start;
        }
    }
}
