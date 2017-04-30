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
                await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await source.Read());
            }
        }

        [Fact]
        public async Task ParenthesisAroundQuotedText()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "(\"test\")"))
            {
                Assert.Equal(TokenType.ExpressionOpen, (await source.Read()).Type);
                Assert.Equal("test", await source.Read());
                Assert.Equal(TokenType.ExpressionClose, (await source.Read()).Type);
            }
        }

        [Fact]
        public async Task ReadFullLineOfTextWithWhitespaces()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, " This is a test \nnot read"))
            {
                string text = await source.ReadTextWhileNot(TokenType.EndOfFile, TokenType.LineBreak);
                Assert.Equal(" This is a test ", text);
            }

        }

        [Fact]
        public async Task ReadQuotedEmptyStringFollowedBySpace()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\"\" "))
            {
                Assert.Equal("", await source.Read());
                Assert.Equal(TokenType.EndOfFile, (await source.Read()).Type);
            }
        }


        [Fact]
        public async Task ParenthesisAroundText()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "(test)"))
            {
                Assert.Equal(TokenType.ExpressionOpen, (await source.Read()).Type);
                Assert.Equal("test", await source.Read());
                Assert.Equal(TokenType.ExpressionClose, (await source.Read()).Type);
            }
        }

        [Fact]
        public async Task ReadEmptyQuotedString()
        {
            Assert.Equal("", await GetUnescapedString(@""));
        }

        [Fact]
        public async Task ReadQuotedStringWithSpaces()
        {
            Assert.Equal("t t", await GetUnescapedString(@"t t"));
        }



        [Fact]
        public async Task ReadQuotedStringWithParenthesis()
        {
            Assert.Equal("test (1)", await GetUnescapedString(@"test (1)"));
        }

        [Fact]
        public async Task ReadStringWithoutQuotes()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "NoQuotes"))
            {
                string token = await source.Read();
                Assert.Equal("NoQuotes", token);
            }
        }

        [Fact]
        public async Task ReadStringWithoutQuotesBetweenWhitespace()
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\r \t NoQuotes \t"))
            {
                string token = await source.Read();
                Assert.Equal("NoQuotes", token);
            }
        }

        [Fact]
        public async Task UnescapeDoubleBlackslash()
        {
            Assert.Equal("\\", await GetUnescapedString(@"\\"));
        }

        [Fact]
        public async Task UnescapeDoubleBlackslashWithTrailingCharacter()
        {
            Assert.Equal("\\n", await GetUnescapedString(@"\\n"));
        }

        [Fact]
        public async Task UnescapeLongUnicodeFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await GetUnescapedString(@"\U00000020a"));
        }

        [Fact]
        public async Task UnescapeNewLine()
        {
            Assert.Equal("\n", await GetUnescapedString(@"\n"));
        }

        [Fact]
        public async Task UnescapeOctalSingleDigitFollowedByLetter()
        {
            Assert.Equal("\0a", await GetUnescapedString(@"\0a"));
        }

        [Fact]
        public async Task UnescapeOctalThreeDigits()
        {
            Assert.Equal(" ", await GetUnescapedString(@"\040"));
        }

        [Fact]
        public async Task UnescapeOctalThreeDigitsFollowedByDigit()
        {
            Assert.Equal(" 1", await GetUnescapedString(@"\0401"));
        }

        [Fact]
        public async Task UnescapeOctalTwoDigits()
        {
            Assert.Equal(" ", await GetUnescapedString(@"\40"));
        }

        [Fact]
        public async Task UnescapeOctalTwoDigitsFollowedByLetter()
        {
            Assert.Equal(" a", await GetUnescapedString(@"\40a"));
        }

        [Fact]
        public async Task UnescapeShortUnicodeFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await GetUnescapedString(@"\u0020a"));
        }

        [Fact]
        public async Task UnescapeSurregateLongUnicode()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal("\uD852\uDF62", await GetUnescapedString(@"\U00024B62"));
        }

        [Fact]
        public async Task UnescapeTwoDigitHex()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" ", await GetUnescapedString(@"\x20"));
        }

        [Fact]
        public async Task UnescapeTwoDigitHexFollowedByLetter()
        {
            // TODO: This should fail according to the C standard, so check with KiCad what it will
            //       actually do!
            Assert.Equal(" a", await GetUnescapedString(@"\x20a"));
        }



        private async Task<string> GetUnescapedString(string escapedString)
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\"" + escapedString + "\""))
            {
                return await source.Read();
            }
        }
    }
}