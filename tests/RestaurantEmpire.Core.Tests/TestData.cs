using System;
using System.IO;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Locates the repository's `data/` directory from wherever the test assembly
    /// happens to be running. Walks up rather than copying files to the output
    /// directory, so tests always read the same definition files a real run would.
    /// </summary>
    internal static class TestData
    {
        public static string DataDirectory { get; } = Locate();

        private static string Locate()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "data");
                if (File.Exists(Path.Combine(candidate, "ingredients.json")))
                    return candidate;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the 'data' directory by walking up from " + AppContext.BaseDirectory);
        }
    }
}
