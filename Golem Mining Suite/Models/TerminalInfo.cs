namespace Golem_Mining_Suite.Models
{
    public class TerminalInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StarSystem { get; set; } = string.Empty;

        public string DisplayName => $"{Name} ({StarSystem})";

        public override string ToString() => DisplayName;
    }
}
