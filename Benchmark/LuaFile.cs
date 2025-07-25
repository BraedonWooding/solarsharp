namespace Benchmark
{
    public class LuaFile(string fileName)
    {
        public string FileName { get; set; } = fileName;
        public string Contents { get; set; } = File.ReadAllText(fileName);

        public override string? ToString()
        {
            return Path.GetFileName(FileName);
        }
    }
}
