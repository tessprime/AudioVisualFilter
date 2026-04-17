using System.IO;

namespace AudioVisualFilter.Analyses
{
    public record PraatFrame(double TimeSeconds, double?[] Formants);

    public static class PraatCsvParser
    {
        public static IReadOnlyList<PraatFrame> Parse(string csvPath)
        {
            var lines = File.ReadAllLines(csvPath);
            var frames = new List<PraatFrame>();

            foreach (var line in lines.Skip(1)) // skip header
            {
                var cols = line.Split(',');
                if (cols.Length < 2) continue;

                if (!double.TryParse(cols[0], out double time)) continue;

                var formants = new double?[cols.Length - 1];
                for (int i = 1; i < cols.Length; i++)
                {
                    var cell = cols[i].Trim();
                    formants[i - 1] = cell == "--undefined--" ? null : double.Parse(cell);
                }

                frames.Add(new PraatFrame(time, formants));
            }

            return frames;
        }
    }
}
