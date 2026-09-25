using RapidOcrNet;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = new UTF8Encoding(false);
if (args.Length != 2) { Console.Error.WriteLine("Usage: ShotCab.Ocr <image.png> <result.json>"); return 2; }
try
{
    var input = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    using var engine = new RapidOcr();
    using var options = RapidOcr.GetDefaultSessionOptions(Math.Min(4, Environment.ProcessorCount));
    engine.InitModels(RapidOcrModelSet.PPOCRv6Small, options);
    var result = engine.Detect(input, RapidOcrOptions.PPOCRv6 with { ReturnWordBox = true, ReturnSingleCharBox = true });
    var document = new
    {
        Version = 1, Text = result.StrRes,
        Lines = result.TextBlocks.Select(block => new
        {
            Text = block.Text,
            Points = block.BoxPoints.Select(p => new { p.X, p.Y }),
            Chars = (block.WordResults ?? Array.Empty<WordBox>()).Select(word => new { word.Text, word.Score, Points = word.BoxPoints.Select(p => new { p.X, p.Y }) })
        })
    };
    File.WriteAllText(output, JsonSerializer.Serialize(document), new UTF8Encoding(false));
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
