using KiCadDoxer.Renderer;
using KiCadDoxer.Renderer.Exceptions;
using System.Threading.Tasks;
using Xunit;

namespace Renderer.Tests
{
    public class LineSourceTests
    {
        [Fact]
        public async Task NoNonWhiteSpaceCharacterAfterQuotedText()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\"t\"est"))
            {
                await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await source.ReadToken());
            }
        }

        [Fact]
        public async Task ParenthesisAroundQuotedText()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "(\"test\")"))
            {
                Assert.Equal(TokenType.ExpressionOpen, (await source.ReadToken()).Type);
                Assert.Equal("test", await source.ReadToken());
                Assert.Equal(TokenType.ExpressionClose, (await source.ReadToken()).Type);
            }
        }

        [Fact]
        public async Task ParenthesisAroundText()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "(test)"))
            {
                Assert.Equal(TokenType.ExpressionOpen, (await source.ReadToken()).Type);
                Assert.Equal("test", await source.ReadToken());
                Assert.Equal(TokenType.ExpressionClose, (await source.ReadToken()).Type);
            }
        }

        [Fact]
        public async Task ReadEmptyQuotedString()
        {
            Assert.Equal("", await GetUnescapeString(@""));
        }

        [Fact]
        public async Task ReadStringWithoutQuotes()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "NoQuotes"))
            {
                string token = await source.ReadToken();
                Assert.Equal("NoQuotes", token);
            }
        }

        [Fact]
        public async Task ReadStringWithoutQuotesBetweenWhitespace()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\r\n\t NoQuotes \t"))
            {
                string token = await source.ReadToken();
                Assert.Equal("NoQuotes", token);
            }
        }

        [Fact]
        public async Task UnescapeDoubleBlackslash()
        {
            Assert.Equal("\\", await GetUnescapeString(@"\\"));
        }

        [Fact]
        public async Task UnescapeDoubleBlackslashWithTrailingCharacter()
        {
            Assert.Equal("\\n", await GetUnescapeString(@"\\n"));
        }

        [Fact]
        public async Task UnescapeLongUnicodeFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await GetUnescapeString(@"\U00000020a"));
        }

        [Fact]
        public async Task UnescapeNewLine()
        {
            Assert.Equal("\n", await GetUnescapeString(@"\n"));
        }

        [Fact]
        public async Task UnescapeOctalSingleDigitFollowedByLetter()
        {
            Assert.Equal("\0a", await GetUnescapeString(@"\0a"));
        }

        [Fact]
        public async Task UnescapeOctalThreeDigits()
        {
            Assert.Equal(" ", await GetUnescapeString(@"\040"));
        }

        [Fact]
        public async Task UnescapeOctalThreeDigitsFollowedByDigit()
        {
            Assert.Equal(" 1", await GetUnescapeString(@"\0401"));
        }

        [Fact]
        public async Task UnescapeOctalTwoDigits()
        {
            Assert.Equal(" ", await GetUnescapeString(@"\40"));
        }

        [Fact]
        public async Task UnescapeOctalTwoDigitsFollowedByLetter()
        {
            Assert.Equal(" a", await GetUnescapeString(@"\40a"));
        }

        [Fact]
        public async Task UnescapeShortUnicodeFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await GetUnescapeString(@"\u0020a"));
        }

        [Fact]
        public async Task UnescapeSurregateLongUnicode()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal("\uD852\uDF62", await GetUnescapeString(@"\U00024B62"));
        }

        [Fact]
        public async Task UnescapeTwoDigitHex()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" ", await GetUnescapeString(@"\x20"));
        }

        [Fact]
        public async Task UnescapeTwoDigitHexFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await GetUnescapeString(@"\x20a"));
        }

        private async Task<string> GetUnescapeString(string value)
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\"" + value + "\""))
            {
                return await source.ReadToken();
            }
        }
    }
}