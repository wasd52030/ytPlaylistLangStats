public static class Extensions
{
    public static string FixedString(this string source, int length)
    {
        return source == null ? new string(' ', length) : source.PadRight(length).Substring(0, length);
    }
}