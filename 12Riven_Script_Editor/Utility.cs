using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Csv;
using System.IO;
using System.Windows.Forms;

namespace Riven_Script_Editor
{
    static class Utility
    {
        //static readonly string encoding = "Shift-JIS";
        static readonly string encoding = "Big5";
        static byte[] fontWidthInfo;
        static Dictionary<char,int> characterWidths = new Dictionary<char, int>();
        static Dictionary<char,int> characterWidthsWithPadding = new Dictionary<char, int>();

        static Regex lineBreakRegex = new Regex(@"([^%\s](?:[\p{L}0-9,.\-'""!?ΑαΒβΓγΔδΕεΖζΗηΘθΙιΚκΛλΜμΝνΞξΟοΠπΡρΣσςΤτΥυΦφΧχΨψΩω]|(?:%[^N])+(?:%\B)*)*)|(%(?:[ABDEKNPpSV]|L[RC]|F[SE]|[OTX][0-9]+|TS[0-9]+|TE|C[0-9A-F]{4})+?)|(\s+)");

        private static bool _fontWidthFileLoaded = false;

        public static void LoadFontWidthFile(string filename)
        {
            if (!File.Exists(filename))
                return;

            fontWidthInfo = File.ReadAllBytes(filename);
            
            // old csv format
            /*using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                
                var line = CsvReader.ReadFromStream(fs, new CsvOptions() { HeaderMode = HeaderMode.HeaderAbsent }).First();
                fontGlyphWidths = line.Values.Select(v => string.IsNullOrEmpty(v) ? 0 : Int32.Parse(v)).ToList();
            }*/

            _fontWidthFileLoaded = true;
            characterWidths.Clear();
            characterWidthsWithPadding.Clear();
        }

        public static bool IsFontWidthFileLoaded() { return _fontWidthFileLoaded; }

        public static int GetFontGlyphIndex(char c)
        {
            int halfWidthOffset = 188 * 3; // maybe temporary because base font is half-width for some reason?
            int fontOffset = -1;
            if ((c >= '!' && c <= '~') || (c >= 'a' && c <= 'z'))
                fontOffset = (c - '!') + 188 + halfWidthOffset;
            else if (c >= 'Α' && c <= 'Ω')
                fontOffset = (c - 'Α') + 470;
            else if (c >= 'А' && c <= 'Я')
                fontOffset = (c - 'А') + 564;
            else if (c >= 'а' && c <= 'н')
                fontOffset = (c - 'а') + 612;
            else if (c >= 'о' && c <= 'я')
                fontOffset = (c - 'о') + 627;
            else if (c == ' ')
                fontOffset = 345 + halfWidthOffset;
            return fontOffset;
        }

        public static int GetCharacterWidth(char c)
        {
            int width;
            if (!characterWidths.TryGetValue(c, out width))
            {
                int i = GetFontGlyphIndex(c);

                if (i != -1)
                    width = fontWidthInfo[i*4+1] - fontWidthInfo[i*4];

                characterWidths[c] = width;
            }
            return width;
        }

        public static int GetCharacterWidthWithPadding(char c)
        {
            int width;
            if (!characterWidthsWithPadding.TryGetValue(c, out width))
            {
                int i = GetFontGlyphIndex(c);

                if (i != -1)
                    width = GetCharacterWidth(c) + fontWidthInfo[i * 4 + 2];

                characterWidthsWithPadding[c] = width;
            }
            return width;
        }

        public static int GetWordWidth(string s)
        {
            return s.Sum(c => GetCharacterWidthWithPadding(c));
        }

        public static int GetWordWidthEOL(string s)
        {
            return s.Substring(0, s.Length - 1).Sum(c => GetCharacterWidthWithPadding(c)) + GetCharacterWidth(s[s.Length-1]);
        }

        public static string AddLineBreaks(string s, bool isDialogue = false)
        {
            if (!IsFontWidthFileLoaded())
                return s;

            // int maxCharsPerLine = 48; // max chars allowable for the backlog
            int maxCharsPerLine = 999; // temporarily "disabled" to focus on pixel width
            int maxPixelsPerLine = 545; // estimate of pixels allowable for the backlog (since it's less wide than the actual textbox)

            List<string> lines = new List<string>();
            int curLineChars = 0;
            int curLineWidth = 0;
            string trailingWhitespace = "";
            int trailingWhitespaceWidth = 0;
            string curLine = "";
            if (isDialogue) s = "“" + s;

            foreach (Match match in lineBreakRegex.Matches(s))
            {
                bool isWord = match.Groups[1].Success;
                bool isCommandTag = match.Groups[2].Success;
                bool isNewline = match.Groups[2].Value == "%N";
                bool isWhitespace = match.Groups[3].Success;
                
                if (isWord)
                {
                    int wordWidthEOL = GetWordWidthEOL(match.Value);

                    if (curLineWidth + trailingWhitespaceWidth + wordWidthEOL >= maxPixelsPerLine || 
                        curLineChars + trailingWhitespace.Length + match.Value.Length >= maxCharsPerLine)
                    {
                        lines.Add(curLine);
                        curLine = "";
                        curLineChars = 0;
                        curLineWidth = 0;
                        trailingWhitespace = "";
                        trailingWhitespaceWidth = 0;
                    }

                    int wordWidth = GetWordWidth(match.Value);

                    curLineWidth += trailingWhitespaceWidth + wordWidth;
                    curLineChars += trailingWhitespace.Length + match.Value.Length;
                    curLine += trailingWhitespace + match.Value;
                    trailingWhitespace = "";
                    trailingWhitespaceWidth = 0;
                }
                else if (isCommandTag && !isNewline)
                {
                    curLine += match.Value;
                }
                else if (isWhitespace)
                {
                    trailingWhitespaceWidth += GetWordWidth(match.Value);
                    trailingWhitespace += match.Value;
                }

                if (isNewline)
                {
                    lines.Add(curLine);
                    curLine = "";
                    curLineChars = 0;
                    curLineWidth = 0;
                    trailingWhitespace = "";
                    trailingWhitespaceWidth = 0;
                }
            }

            if (!string.IsNullOrEmpty(curLine))
                lines.Add(curLine);

            if (isDialogue && lines.Count > 0)
                lines[0] = lines[0].Substring(1); // remove first quote if dialogue line

            return string.Join("%N", lines);
        }

        public static string StringSingleSpace(string input)
        {
            return input.Replace("  ", " ");
        }

        public static string StringDoubleSpace(string input)
        {
            return input.Replace(" ", "  ");
        }

        public static byte[] StringEncode(string input)
        {
            string temp = input.Replace("ï", "∇");
            temp = temp.Replace("é", "≡");
            temp = temp.Replace("ö", "≒");
            var x = Encoding.GetEncoding(encoding).GetBytes(temp);

            for (int i = 0; i < x.Length - 1; i++)
                if (x[i] >= 0x80)
                {
                    i++;
                    if (x[i - 1] == 0x81 && x[i] == 0xe0) // ≒ -> ö
                    { x[i - 1] = 0x86; x[i] = 0x40; }
                    if (x[i - 1] == 0x81 && x[i] == 0xde) // ∇ -> ï
                    { x[i - 1] = 0x86; x[i] = 0x43; }
                    else if (x[i - 1] == 0x81 && x[i] == 0xdf) // ≡ -> é 
                    { x[i - 1] = 0x86; x[i] = 0x44; }
                    else if (x[i - 1] == 0x81 && x[i] == 0x61) // ∥ -> "I"
                    { x[i - 1] = 0x86; x[i] = 0x78; }
                    else if (x[i - 1] == 0x83 && x[i] == 0xB1) // Tau -> "t"
                    { x[i - 1] = 0x86; x[i] = 0xA4; }
                    else if (x[i - 1] == 0x83 && x[i] == 0xA5) // Eta -> "h"
                    { x[i - 1] = 0x86; x[i] = 0x98; }
                    else if (x[i - 1] == 0x83 && x[i] == 0x9F) // Alpha -> "a"
                    { x[i - 1] = 0x86; x[i] = 0x91; }
                    else if (x[i - 1] == 0x81 && x[i] == 0xAB) // ↓ -> "!"
                    { x[i - 1] = 0x86; x[i] = 0x50; }
                    //else if (x[i-1] == 0x81 && x[i] == 0x79) // 【 -> "「" 
                    //    { x[i-1] = 0x85; x[i] = 0xA0; }
                    //else if (x[i-1] == 0x81 && x[i] == 0x7A) // 】 -> "」"
                    //    { x[i-1] = 0x85; x[i] = 0xA1; }

                }

            return x;
        }

        public static string StringDecode(byte[] x)
        {
            for (int i = 0; i < x.Length - 1; i++)
                if (x[i] >= 0x80)
                {
                    i++;
                    if (x[i - 1] == 0x86 && x[i] == 0x40) // ö -> ≒
                    { x[i - 1] = 0x81; x[i] = 0xe0; }
                    else if (x[i - 1] == 0x86 && x[i] == 0x43) // ï -> ∇
                    { x[i - 1] = 0x81; x[i] = 0xde; }
                    else if (x[i - 1] == 0x86 && x[i] == 0x44) // é -> ≡
                    { x[i - 1] = 0x81; x[i] = 0xdf; }
                    else if (x[i - 1] == 0x86 && x[i] == 0x78) // "I" -> ∥
                    { x[i - 1] = 0x81; x[i] = 0x61; }
                    else if (x[i - 1] == 0x86 && x[i] == 0xA4) // "t" -> Tau
                    { x[i - 1] = 0x83; x[i] = 0xB1; }
                    else if (x[i - 1] == 0x86 && x[i] == 0x98) // "h" -> Eta
                    { x[i - 1] = 0x83; x[i] = 0xA5; }
                    else if (x[i - 1] == 0x86 && x[i] == 0x91) // "a" -> Alpha
                    { x[i - 1] = 0x83; x[i] = 0x9F; }
                    else if (x[i - 1] == 0x86 && x[i] == 0x50) // "!" -> ↓
                    { x[i - 1] = 0x81; x[i] = 0xAB; }
                    //else if (x[i-1] == 0x85 && x[i] == 0xA0) //  "「" -> 【
                    //    { x[i-1] = 0x81; x[i] = 0x79; }
                    //else if (x[i-1] == 0x85 && x[i] == 0xA1) // "」" -> 】
                    //    { x[i-1] = 0x81; x[i] = 0x7A; }
                }

            var output = Encoding.GetEncoding(encoding).GetString(x);
            output = output.Replace("∇", "ï");
            output = output.Replace("≡", "é");
            output = output.Replace("≒", "ö");

            return output;
        }

        public static string ToString(byte[] byteArray)
        {
            string byteCommandString = "";
            foreach (byte commandByte in byteArray)
            {
                byteCommandString += commandByte.ToString("X2") + " ";
            }
            return byteCommandString;
        }

        public static bool ToByteArray(string byteString, out byte[] byteArray)
        {
            List<byte> bytes = new List<byte>();
            Regex rgx = new Regex("^[A-Fa-f0-9\\s]*$");

            byteArray = bytes.ToArray();
            if (!rgx.IsMatch(byteString)) { return false; }

            List<string> byteStrings = byteString.Split(' ').ToList();

            byteStrings.RemoveAll(literal => literal.Equals(""));
            foreach (string byteChunk in byteStrings)
            {
                try
                {
                    bytes.Add(Convert.ToByte(int.Parse(byteChunk, System.Globalization.NumberStyles.HexNumber)));
                }
                catch(Exception e)
                {
                    return false;
                }
            }

            byteArray = bytes.ToArray();

            return true;
        }
    }
}