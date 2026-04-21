using FluentAssertions;
using Golem_Mining_Suite.Models;
using Xunit;

namespace Golem.Mining.Suite.Tests.Models
{
    /// <summary>
    /// Boundary tests for <see cref="QualityScore"/>. The thresholds are user-visible (tier
    /// badges in the refinery UI) and cross-referenced from R1-refinery-4.7.md — regressing
    /// a boundary by one point shifts a user's output into the wrong tier.
    /// </summary>
    public class QualityScoreTests
    {
        [Theory]
        [InlineData(-5, 0)]       // clamped up to the floor
        [InlineData(0, 0)]
        [InlineData(500, 500)]
        [InlineData(1000, 1000)]
        [InlineData(1500, 1000)]  // clamped down to the ceiling
        [InlineData(int.MinValue, 0)]
        [InlineData(int.MaxValue, 1000)]
        public void Ctor_ClampsValueIntoRange(int input, int expected)
        {
            var q = new QualityScore(input);
            q.Value.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, QualityTier.Debuff)]
        [InlineData(250, QualityTier.Debuff)]
        [InlineData(499, QualityTier.Debuff)]
        [InlineData(500, QualityTier.Baseline)]
        [InlineData(600, QualityTier.Baseline)]
        [InlineData(649, QualityTier.Baseline)]
        [InlineData(650, QualityTier.Good)]
        [InlineData(675, QualityTier.Good)]
        [InlineData(699, QualityTier.Good)]
        [InlineData(700, QualityTier.Keeper)]
        [InlineData(800, QualityTier.Keeper)]
        [InlineData(899, QualityTier.Keeper)]
        [InlineData(900, QualityTier.Endgame)]
        [InlineData(950, QualityTier.Endgame)]
        [InlineData(1000, QualityTier.Endgame)]
        public void Tier_MapsValueToExpectedBand(int value, QualityTier expected)
        {
            new QualityScore(value).Tier.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(499, true)]
        [InlineData(500, false)]
        [InlineData(700, false)]
        [InlineData(1000, false)]
        public void IsDebuff_IsTrueBelow500(int value, bool expected)
        {
            new QualityScore(value).IsDebuff.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(699, false)]
        [InlineData(700, true)]
        [InlineData(899, true)]
        [InlineData(900, true)]
        [InlineData(1000, true)]
        public void IsKeeper_IsTrueAtOrAbove700(int value, bool expected)
        {
            new QualityScore(value).IsKeeper.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(899, false)]
        [InlineData(900, true)]
        [InlineData(1000, true)]
        public void IsEndgame_IsTrueAtOrAbove900(int value, bool expected)
        {
            new QualityScore(value).IsEndgame.Should().Be(expected);
        }

        [Fact]
        public void ValueEquality_MatchesRecordStructSemantics()
        {
            // Two scores with the same clamped value must compare equal — downstream code
            // (e.g. grouping stacks by quality tier) relies on value equality.
            var a = new QualityScore(650);
            var b = new QualityScore(650);
            var c = new QualityScore(700);

            a.Should().Be(b);
            a.Should().NotBe(c);
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Clamping_PreservesEqualityForOutOfRangeInputs()
        {
            // -5 and 0 both clamp to 0 → must be equal.
            new QualityScore(-5).Should().Be(new QualityScore(0));
            // 1500 and 1000 both clamp to 1000.
            new QualityScore(1500).Should().Be(new QualityScore(1000));
        }
    }
}
