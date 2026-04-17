using AudioVisualFilter.Analyses;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: FormantAnalyzer <wav-dir> <out-dir>");
    return 1;
}

string wavDir = args[0];
string outDir = args[1];

if (!Directory.Exists(wavDir))
{
    Console.Error.WriteLine($"WAV directory not found: {wavDir}");
    return 1;
}

Directory.CreateDirectory(outDir);

var config = new AnalysisConfig();
var wavs = Directory.GetFiles(wavDir, "*.wav");

if (wavs.Length == 0)
{
    Console.Error.WriteLine($"No WAV files found in {wavDir}");
    return 1;
}

foreach (var wav in wavs)
{
    string name   = Path.GetFileNameWithoutExtension(wav);
    string outCsv = Path.Combine(outDir, name + ".csv");

    Console.Write($"Analyzing {name}... ");
    var frames = WavAnalyzer.Analyze(wav, config);
    WavAnalyzer.WriteCsv(frames, outCsv);
    Console.WriteLine($"{frames.Count} frames -> {outCsv}");
}

return 0;
