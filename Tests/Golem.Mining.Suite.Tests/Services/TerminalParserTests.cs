using FluentAssertions;
using Golem_Mining_Suite.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Pre-4.7 baseline tests for TerminalParser. The parser is pure (no HTTP, only regex over
    /// OCR text and optional file logging), so we feed it hand-crafted text that matches the
    /// regex patterns in TerminalParser.cs and lock in the fields it extracts.
    /// </summary>
    public class TerminalParserTests
    {
        private static TerminalParser NewSut() => new TerminalParser(NullLogger<TerminalParser>.Instance);

        [Fact]
        public void ParseTerminalText_NullOrWhitespace_ReturnsNull()
        {
            var sut = NewSut();

            sut.ParseTerminalText(null!).Should().BeNull();
            sut.ParseTerminalText(string.Empty).Should().BeNull();
            sut.ParseTerminalText("   \r\n  ").Should().BeNull();
        }

        [Fact]
        public void ParseTerminalText_GibberishWithNoKnownCommodity_ReturnsNull()
        {
            // Negative test: parser must not throw on gibberish input.
            var sut = NewSut();

            var result = sut.ParseTerminalText("xxxxx qwerty zzz 12345 !!!!\n~~~~ nothing useful here ~~~~");

            result.Should().BeNull("no known mineral name means no commodity, which short-circuits to null");
        }

        [Fact]
        public void ParseTerminalText_QuantaniumWithKPerScuPrice_ParsesCommodityAndPrice()
        {
            // "£88.0K/SCU" style — K/SCU with a currency prefix is the most common UEX terminal format.
            // Expected: 88.0 * 1000 = 88,000 aUEC.
            string ocr = """
            Area 18 Trade Terminal
            Quantanium £88.0K/SCU
            MAX INVENTORY 18,000 SCU
            """;
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.CommodityName.Should().Be("Quantanium");
            // Both price slots pick up the same number (parser scans the commodity context for either "sell" or "buy" — the pattern is position-agnostic).
            result.PriceSell.Should().Be(88000);
            result.InventoryMax.Should().Be(18000);
        }

        [Fact]
        public void ParseTerminalText_LaranitePlainKPerScu_ParsesPrice()
        {
            // "21.8K/SCU" — just a number + K/SCU, no currency prefix.
            string ocr = """
            Port Olisar
            Laranite 21.8K/SCU
            """;
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.CommodityName.Should().Be("Laranite");
            result.PriceSell.Should().Be(21800);
        }

        [Fact]
        public void ParseTerminalText_TerminalNameKeyword_IsCaptured()
        {
            // TerminalParser.ExtractTerminalName returns the first line containing any of the
            // keywords: Port, Area, Station, Terminal, Lorville, Orison, New Babbage.
            string ocr = """
            New Babbage Trade
            Titanium 4.9K/SCU
            """;
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.TerminalName.Should().Contain("New Babbage");
            result.CommodityName.Should().Be("Titanium");
        }

        [Fact]
        public void ParseTerminalText_NoTerminalKeyword_FallsBackToUnknownTerminal()
        {
            // When no terminal keyword is present, ExtractTerminalName returns the literal "Unknown Terminal".
            string ocr = "Copper 4.1K/SCU";
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.CommodityName.Should().Be("Copper");
            result.TerminalName.Should().Be("Unknown Terminal");
        }

        [Fact]
        public void ParseTerminalText_OcrDecimalAsSpace_IsNormalized()
        {
            // OCR sometimes reads "88.0K/SCU" as "88 0K/SCU" — parser normalizes the space to a decimal.
            // See RefineryService sibling logic; TerminalParser does the same at ExtractPrice line 139.
            string ocr = """
            Area 18
            Quantanium 88 0K/SCU
            """;
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.PriceSell.Should().Be(88000);
        }

        [Fact]
        public void ParseTerminalText_LegacyCurrentOverMaxInventory_IsCaptured()
        {
            // Legacy "current/max" pattern: "1,200/5,000" → current=1200, max=5000.
            // Note: commodity context is only commodity-line + next 3 lines, so the inventory line must be near the commodity.
            string ocr = """
            Area 18 Trade Terminal
            Iron 2.4K/SCU
            1,200 / 5,000 SCU
            """;
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.CommodityName.Should().Be("Iron");
            result.InventorySCU.Should().Be(1200);
            result.InventoryMax.Should().Be(5000);
        }

        [Fact]
        public void ParseTerminalText_GibberishAroundRealCommodity_StillParses()
        {
            // Negative-ish test: even with noise, a real commodity name must still be found and
            // the parser must not throw — partial population is acceptable.
            string ocr = """
            %%%% random %% garbage %%%%
            Gold 7.6K/SCU
            !!!!!!! nonsense !!!!!
            """;
            var sut = NewSut();

            var result = sut.ParseTerminalText(ocr);

            result.Should().NotBeNull();
            result!.CommodityName.Should().Be("Gold");
            result.PriceSell.Should().Be(7600);
        }

        [Fact]
        public void TerminalData_GetDemand_ReflectsInventoryPercent()
        {
            // Sanity-check the consumer of parser output — GetDemand() is what the rest of the app calls.
            var low = new Golem_Mining_Suite.Models.TerminalData
            {
                CommodityName = "Iron",
                InventorySCU = 100,
                InventoryMax = 1000
            };
            var high = new Golem_Mining_Suite.Models.TerminalData
            {
                CommodityName = "Iron",
                InventorySCU = 900,
                InventoryMax = 1000
            };
            var zero = new Golem_Mining_Suite.Models.TerminalData
            {
                CommodityName = "Iron",
                InventorySCU = 0,
                InventoryMax = 0
            };

            low.GetDemand().Should().Be("High");
            high.GetDemand().Should().Be("Low");
            zero.GetDemand().Should().Be("Unknown");
        }
    }
}
