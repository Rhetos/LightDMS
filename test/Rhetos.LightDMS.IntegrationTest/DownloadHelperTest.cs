/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class DownloadHelperTest
    {
        private readonly ITestOutputHelper _output;

        public DownloadHelperTest(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        static string[] _filenames =
        {
            "abc",
            "abc ",
            "abcčćđČĆĐ",
            "abc`~!@#$%^&*()_+-=?[]\\{}|;':\",./<>",
            "abcč`~!@#$%^&*()_+-=?[]\\{}|;':\",./<>č∞",
            "abc∞",
        };

        public static IEnumerable<object[]> Filenames => _filenames.Select(f => new[] { f });

        [Theory]
        [MemberData(nameof(Filenames))]
        public void EscapeFilename_Reconstruction(string test)
        {
            string escaped = DownloadHelper.EscapeFilename(test);
            _output.WriteLine($"escaped: {escaped}");
            string reconstruction = Uri.UnescapeDataString(escaped);
            Assert.Equal(test, reconstruction);
        }

        static bool IsAlphanumeric(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

        /// <summary>
        /// RFC 5987 https://datatracker.ietf.org/doc/html/rfc5987 specifies the usage of "UTF-8''" prefix and the character encoding,
        /// see "Inside the value part, characters not contained in attr-char are encoded".
        /// </summary>
        static readonly HashSet<char> allowedSpecialCharacters = "!#$&+-.^_`|~".ToHashSet();

        [Theory]
        [MemberData(nameof(Filenames))]
        public void EscapeFilename_AllowedCharacters(string test)
        {
            HashSet<char> unescapedSpecialChar = new();

            for (int i = 0; i < test.Length; i++)
            {
                string t = test[i..(i + 1)];
                string e = DownloadHelper.EscapeFilename(t);
                _output.WriteLine($"{t} => {e}");

                if (IsAlphanumeric(t.Single()))
                    Assert.Equal(t, e);
                else if (t == e)
                    unescapedSpecialChar.Add(t.Single());
                else
                    Assert.StartsWith("%", e);
            }

            // Not allowed characters that were not escaped.
            Assert.Equal("", string.Join(" ", unescapedSpecialChar.Except(allowedSpecialCharacters).OrderBy(x => x)));
        }

        [Theory]
        [MemberData(nameof(Filenames))]
        public void EscapeFilename_AllowedCharactersFullString(string test)
        {
            string escaped = DownloadHelper.EscapeFilename(test);
            _output.WriteLine($"escaped: {escaped}");
            var unescaped = escaped.Where(c => !IsAlphanumeric(c) && c != '%')
                .Except(allowedSpecialCharacters)
                .Distinct().OrderBy(c => c).ToList();
            Assert.Empty(unescaped);
        }
    }
}
