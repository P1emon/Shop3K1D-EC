using System.Text.RegularExpressions;

namespace MyEStore.Helpers
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            input = input.ToLower();
            input = System.Text.RegularExpressions.Regex.Replace(input, @"[^a-z0-9\s-]", "");
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\s+", " ").Trim();
            input = input.Replace(" ", "-");

            return input;
        }
    }

}
