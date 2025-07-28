namespace Benchmark;

public class LuaFile
{
    public LuaFile(string fileName)
    {
        FileName = fileName;
        Contents = File.ReadAllText(fileName);
    }

    public string FileName { get; set; }
    public string Contents { get; set; }

    public override string? ToString()
    {
        return Path.GetFileName(FileName);
    }
}