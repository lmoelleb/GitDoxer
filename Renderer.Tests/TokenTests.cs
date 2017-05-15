using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests
{
    public class TokenTests
    {
        [Fact]
        public async Task UnescapeDoubleBlackslash()
        {
            Assert.Equal("\\", await StringLineSource.GetUnescapedString(@"\\"));
        }

        [Fact]
        public async Task UnescapeDoubleBlackslashWithTrailingCharacter()
        {
            Assert.Equal("\\n", await StringLineSource.GetUnescapedString(@"\\n"));
        }

        [Fact]
        public async Task UnescapeLineOfTextIncludesEnclosingQuotes()
        {
            Assert.Equal("\"test\"text\n\"", await StringLineSource.GetUnescapedLineString("\"test\"text\\n\""));
        }

        [Fact]
        public async Task UnescapeLongUnicodeFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await StringLineSource.GetUnescapedString(@"\U00000020a"));
        }

        [Fact]
        public async Task UnescapeNewLine()
        {
            Assert.Equal("\n", await StringLineSource.GetUnescapedString(@"\n"));
        }

        [Fact]
        public async Task UnescapeOctalSingleDigitFollowedByLetter()
        {
            Assert.Equal("\0a", await StringLineSource.GetUnescapedString(@"\0a"));
        }

        [Fact]
        public async Task UnescapeOctalThreeDigits()
        {
            Assert.Equal(" ", await StringLineSource.GetUnescapedString(@"\040"));
        }

        [Fact]
        public async Task UnescapeOctalThreeDigitsFollowedByDigit()
        {
            Assert.Equal(" 1", await StringLineSource.GetUnescapedString(@"\0401"));
        }

        [Fact]
        public async Task UnescapeOctalTwoDigits()
        {
            Assert.Equal(" ", await StringLineSource.GetUnescapedString(@"\40"));
        }

        [Fact]
        public async Task UnescapeOctalTwoDigitsFollowedByLetter()
        {
            Assert.Equal(" a", await StringLineSource.GetUnescapedString(@"\40a"));
        }

        [Fact]
        public async Task UnescapeShortUnicodeFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await StringLineSource.GetUnescapedString(@"\u0020a"));
        }

        [Fact]
        public async Task UnescapeSurregateLongUnicode()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal("\uD852\uDF62", await StringLineSource.GetUnescapedString(@"\U00024B62"));
        }

        [Fact]
        public async Task UnescapeThreeDigitHexFollowedByNonHexLetter()
        {
            Assert.Equal("\x20ag", await StringLineSource.GetUnescapedString(@"\x20ag"));
        }

        [Fact]
        public async Task UnescapeTwoDigitHex()
        {
            Assert.Equal(" ", await StringLineSource.GetUnescapedString(@"\x20"));
        }
    }
}