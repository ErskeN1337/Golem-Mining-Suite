using System.Text.RegularExpressions;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Compiled regex patterns for parsing Star Citizen <c>Game.log</c> lines.
    /// <para>
    /// Patterns are ported from the community-maintained parsers surveyed in
    /// <c>tasks/research/R2-game-log-mining.md</c> — primarily AutoTrackR2 (MIT), all-slain (MIT),
    /// and StarLogs (MIT). They are kept in a dedicated class (separate from
    /// <see cref="GameLogService"/>) so tests can target them directly without spinning up a
    /// file-tailing loop.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Only session-bracketing signals are represented here. Mining-mechanic events
    /// (rock scan composition, fracture outcome, mined cargo quantity, etc.) are not present in
    /// <c>Game.log</c> post-4.0.2 and must be captured via OCR.
    /// </remarks>
    public static class SCLogPatterns
    {
        private const RegexOptions DefaultOptions =
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

        /// <summary>
        /// Matches the ISO-8601 UTC timestamp wrapped in angle brackets at the start of most
        /// <c>Game.log</c> lines, capturing the remainder of the line in <c>rest</c>.
        /// </summary>
        public static readonly Regex Timestamp = new(
            @"^<(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3})Z>\s*(?<rest>.*)$",
            DefaultOptions);

        /// <summary>
        /// <c>&lt;Actor Death&gt;</c> — fires when any actor is killed; the service filters for
        /// the local player by comparing <c>victim</c> against the most recently observed login
        /// handle.
        /// </summary>
        public static readonly Regex ActorDeath = new(
            @"\[Notice\] <Actor Death> CActor::Kill: '(?<victim>[\w-]+)' \[\d+\] in zone '(?<zone>[\w-]+)' killed by '(?<killer>[\w-]+)' \[\d+\] using '(?<weapon>[\w-]+)' \[Class (?<weaponClass>[\w-]+)\] with damage type '(?<damageType>[A-Za-z]+)'",
            DefaultOptions);

        /// <summary>
        /// <c>&lt;Vehicle Destruction&gt;</c> — captures vehicle name, zone, driver, and the
        /// before/after destroy levels. Level 2 = full destruction.
        /// </summary>
        public static readonly Regex VehicleDestruction = new(
            @"<Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle '(?<vehicle>[^']+)' \[\d+\] in zone '(?<zone>[^']+)' \[pos x: (?<x>[-\d\.]+), y: (?<y>[-\d\.]+), z: (?<z>[-\d\.]+) vel x: [^,]+, y: [^,]+, z: [^\]]+\] driven by '(?<driver>[^']+)' \[\d+\] advanced from destroy level (?<levelFrom>\d+) to (?<levelTo>\d+) caused by '(?<causedBy>[^']+)' \[\d+\] with '(?<damageType>[^']+)'",
            DefaultOptions);

        /// <summary>
        /// <c>&lt;Actor stall&gt;</c> — a downstream/upstream stall; severe stalls (>15s)
        /// followed by a <see cref="Disconnect"/> within ~60s indicate a likely 30k.
        /// </summary>
        public static readonly Regex ActorStall = new(
            @"\[Notice\] <Actor stall> Actor stall detected, Player: (?<player>[\w-]+), Type: (?<direction>(?:up|down)stream), Length: (?<seconds>\d+\.\d+)\. \[Team_ActorTech\]\[Actor\]",
            DefaultOptions);

        /// <summary>
        /// "-- Entity Trying To QT: &lt;name&gt;" — Quantum travel start (SC 4.0.1+).
        /// </summary>
        public static readonly Regex QuantumStart = new(
            @"-- Entity Trying To QT:\s*(?<who>.+)$",
            DefaultOptions);

        /// <summary>
        /// <c>&lt;Jump Drive State Changed&gt;</c> — used both to identify the currently-flown
        /// ship and to detect QT end (state == <c>Idle</c> after a prior QT start).
        /// </summary>
        public static readonly Regex JumpDriveState = new(
            @"<Jump Drive State Changed>.*?[Aa]dam:\s*(?<shipClass>[A-Z]{3,5}_[\w]+?)_(?<shipId>\d+)\s+in\s+(?<state>\w+)",
            DefaultOptions);

        /// <summary>
        /// <c>&lt;Legacy login response&gt;</c> — primary source of the local player's RSI handle
        /// on every sign-in.
        /// </summary>
        public static readonly Regex LegacyLogin = new(
            @"\[Notice\] <Legacy login response> \[CIG-net\] User Login Success - Handle\[(?<player>[A-Za-z0-9_-]+)\]",
            DefaultOptions);

        /// <summary>
        /// <c>&lt;AccountLoginCharacterStatus_Character&gt;</c> — backup login signal, used when
        /// the Legacy line is missing.
        /// </summary>
        public static readonly Regex CharacterLogin = new(
            @"\[Notice\] <AccountLoginCharacterStatus_Character> Character: createdAt \d+ - updatedAt \d+ - geid \d+ - accountId \d+ - name (?<name>[\w-]+) - state STATE_(?:CURRENT|UNSPECIFIED)",
            DefaultOptions);

        /// <summary>Lone "Disconnect" line emitted on clean session end.</summary>
        public static readonly Regex Disconnect = new(
            @"^\s*[Dd]isconnect\s*$",
            DefaultOptions);

        /// <summary><c>&lt;SystemQuit&gt; CSystem::Quit invoked</c>.</summary>
        public static readonly Regex SystemQuit = new(
            @"\[Notice\] <SystemQuit> CSystem::Quit invoked",
            DefaultOptions);

        /// <summary><c>&lt;CDisciplineServiceExternal::EndSession&gt; Ending session</c>.</summary>
        public static readonly Regex EndSession = new(
            @"\[Notice\] <CDisciplineServiceExternal::EndSession> Ending session",
            DefaultOptions);

        /// <summary>
        /// <c>StopLoadingScreen</c> transition — the player finished loading into the PU.
        /// Carries a <c>sessionId</c> useful for correlating with server-side logs.
        /// </summary>
        public static readonly Regex PuReady = new(
            @"\[Notice\] <ContextEstablisherTaskFinished> establisher=""CReplicationModel"" message=""CET completed"" taskname=""StopLoadingScreen"" .*? sessionId=""(?<sessionId>[a-f0-9\-]+)""",
            DefaultOptions);
    }
}
