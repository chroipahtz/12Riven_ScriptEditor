using Csv;
using Riven_Script_Editor.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Riven_Script_Editor
{

    public class ScriptFileException : Exception
    {
        public ScriptFileException(string message) : base(message)
        {
        }

        public ScriptFileException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class ScriptFile
    {
        public string Name;
        public ATokenizer Tokenizer;
        public List<Token> TokenList;
        public bool ChangedFile = false;

        public static ScriptListFileManager scriptListFileManager;

        public static ScriptFile Load(string path, string pathJp)
        {
            ScriptFile scriptFile = new ScriptFile();

            byte[] binData = File.ReadAllBytes(path);

            string filename = Path.GetFileName(path);

            scriptFile.Name = filename;

            // need better way to determine this
            if (filename.Equals("DATA.BIN"))
            {
                scriptFile.Tokenizer = new DataTokenizer(new DataWrapper(binData));
            }
            else
            {
                scriptFile.Tokenizer = new ScriptTokenizer(new DataWrapper(binData), scriptListFileManager);
            }

            scriptFile.TokenList = scriptFile.Tokenizer.ParseData();

            if (File.Exists(pathJp))
            {
                byte[] binDataJp = File.ReadAllBytes(pathJp);
                ATokenizer TokenizerJp;

                if (filename.Equals("DATA.BIN"))
                {
                    TokenizerJp = new DataTokenizer(new DataWrapper(binDataJp));
                }
                else
                {
                    TokenizerJp = new ScriptTokenizer(new DataWrapper(binDataJp), scriptListFileManager);
                }

                List<Token> tokenListJp = TokenizerJp.ParseData();

                for (int i = 0; i < scriptFile.TokenList.Count; i++)
                {
                    // quick hack. just assumes indexes are the same. needs to change if we add line-adding functionality. -chroi
                    Token token = scriptFile.TokenList[i];
                    if (token is TokenMsgDisp2 tokenMsgDisp2)
                        tokenMsgDisp2.MessageJp = ((TokenMsgDisp2)tokenListJp[i]).Message;
                }
            }

            return scriptFile;
        }

        public bool ImportCSV(Stream reader, bool addLineBreaks = true)
        {
            // bad. horrible. temporary. but it works. -chroi
            if (this.Name == "DATA.BIN")
            {
                return ImportDataCSV(reader);
            }
            else
            {
                return ImportScriptCSV(reader, addLineBreaks);
            }
        }

        private bool ImportDataCSV(Stream reader)
        {
            // even more fragile than the script import. oh well.
            int i = 0;

            Regex commandNameRegex = new Regex(@"([\s\w]+?):");

            Regex routeNameRegex = new Regex(@"\b[\s\w:]+\b(?=\s*\/\/|$)");

            var skipIndex = 0;

            foreach (var line in CsvReader.ReadFromStream(reader, new CsvOptions() { HeaderMode = HeaderMode.HeaderAbsent }))
            {
                if (skipIndex < 1)
                {
                    skipIndex++;
                    continue;
                }

                Match commandMatch = commandNameRegex.Match(line[0]);
                if (!commandMatch.Success || commandMatch.Groups.Count < 2)
                    continue;

                string commandName = commandMatch.Groups[1].Value;
                if (string.IsNullOrEmpty(commandName))
                    continue;

                while (TokenList[i].Command != commandName && i < TokenList.Count)
                    i++;
                if (i > TokenList.Count)
                    break;

                string newText = line[1];
                if (!string.IsNullOrEmpty(newText))
                {
                    if (TokenList[i] is TokenDataRoute)
                    {
                        TokenDataRoute token = TokenList[i] as TokenDataRoute;

                        MatchCollection matches = routeNameRegex.Matches(newText);
                        if (matches.Count == 2)
                        {
                            token.Route1 = matches[0].Value;
                            token.Route2 = matches[1].Value;
                            token.UpdateData();
                            ChangedFile = true;
                        }
                    }
                    else if (TokenList[i] is TokenDataRoute2)
                    {
                        TokenDataRoute2 token = TokenList[i] as TokenDataRoute2;

                        MatchCollection matches = routeNameRegex.Matches(newText);
                        if (matches.Count == 2)
                        {
                            token.Route1 = matches[0].Value;
                            token.Route2 = matches[1].Value;
                            token.UpdateData();
                            ChangedFile = true;
                        }
                    }
                    else if (TokenList[i] is TokenDataName)
                    {
                        TokenDataName token = TokenList[i] as TokenDataName;
                        token.Name = newText;
                        token.UpdateData();
                        ChangedFile = true;
                    }
                    else if (TokenList[i] is TokenDataString)
                    {
                        TokenDataString token = TokenList[i] as TokenDataString;
                        token.DataString = newText;
                        token.UpdateData();
                        ChangedFile = true;
                    }
                }

                i++;
            }

            return true;
        }

        private bool ImportScriptCSV(Stream reader, bool addLineBreaks = true)
        {
            int i = 0;

            List<Type> countedTokenTypes = new List<Type>() { typeof(TokenMsgDisp2), typeof(TokenSelectDisp2) };
            List<Token> countedTokens = TokenList.Where(l => countedTokenTypes.Contains(l.GetType())).ToList();

            Regex lineNumberRegex = new Regex(@"^\d+(?=\. )");
            Regex selectChoiceRegex = new Regex(@"\s*([^/]+?)\s*(?:/|$)");

            var skipIndex = 0;

            foreach (var line in CsvReader.ReadFromStream(reader, new CsvOptions() { HeaderMode = HeaderMode.HeaderAbsent }))
            {
                if (skipIndex < 1)
                {
                    skipIndex++;
                    continue;
                }
                string lineNumber = lineNumberRegex.Match(line[0]).Value;
                if (string.IsNullOrEmpty(lineNumber))
                    continue;
                i = Convert.ToInt32(lineNumber) - 1;
                if (i >= countedTokens.Count)
                    continue; // should break, but might as well keep going through the file

                string newText = line[1];
                if (!string.IsNullOrEmpty(newText))
                {
                    if (countedTokens[i] is TokenMsgDisp2)
                    {
                        TokenMsgDisp2 msgToken = countedTokens[i] as TokenMsgDisp2;

                        // don't replace italics until export
                        //newText = ReplaceItalics(newText);

                        if (addLineBreaks)
                            newText = Utility.AddLineBreaks(newText, !string.IsNullOrEmpty(msgToken.Speaker));

                        countedTokens[i].GetType().GetProperty("Message").SetValue(countedTokens[i], newText);
                        countedTokens[i].UpdateData();
                        ChangedFile = true;
                    }
                    else if (countedTokens[i] is TokenSelectDisp2)
                    {
                        TokenSelectDisp2 selectToken = countedTokens[i] as TokenSelectDisp2;

                        int choiceIdx = 0;
                        foreach (Match match in selectChoiceRegex.Matches(newText))
                        {
                            if (choiceIdx >= selectToken.Entries.Count) break;
                            selectToken.Entries[choiceIdx].Message = match.Groups[1].Value;
                            choiceIdx++;
                        }

                        if (choiceIdx > 0)
                        {
                            selectToken.UpdateData();
                            ChangedFile = true;
                        }
                    }
                }
            }

            return true;
        }

        private string ReplaceItalics(string text)
        {
            Regex italicRegex = new Regex(@"([^%*](?:[\s\w,.\-'""!?ΑαΒβΓγΔδΕεΖζΗηΘθΙιΚκΛλΜμΝνΞξΟοΠπΡρΣσςΤτΥυΦφΧχΨψΩω]+(?:%\B)*)*)|(\*)|(%(?:[ABDEKNPpSV]|L[RC]|F[SE]|[OTX][0-9]+|TS[0-9]+|TE|C[0-9A-F]{4})+?)");

            bool isItalic = false;

            string newText = "";

            foreach (Match match in italicRegex.Matches(text))
            {
                bool isText = match.Groups[1].Success;
                isItalic ^= match.Groups[2].Success;
                bool isCommandTag = match.Groups[3].Success;

                if (isText && isItalic)
                {
                    List<byte> bytes = new List<byte>();

                    foreach (char c in match.Value)
                    {
                        if (c >= 'A' && c <= 'L')
                        {
                            bytes.Add(0xc7);
                            bytes.Add((byte)(0xf3 + (c - 'A')));
                        }
                        else if (c >= 'M' && c <= 'Z')
                        {
                            bytes.Add(0xc8);
                            bytes.Add((byte)(0x40 + (c - 'M')));
                        }
                        else if (c >= 'a' && c <= 'f')
                        {
                            bytes.Add(0xc8);
                            bytes.Add((byte)(0x55 + (c - 'a')));
                        }
                        else if (c >= 'g' && c <= 'z')
                        {
                            bytes.Add(0xc8);
                            bytes.Add((byte)(0x5c + (c - 'g')));
                        }
                        else
                            bytes.Add((byte)c);
                    }

                    newText += Utility.StringDecode(bytes.ToArray());
                }
                else if (isText || isCommandTag)
                {
                    newText += match.Value;
                }
            }

            return newText;
        }

        public bool SaveScriptFile(string filename, string encoding = null, bool processText = false)
        {
            if (filename == "") return false;

            string previousEncoding = null;
            bool saved = false;

            if (encoding == null) encoding = "Shift-JIS";
            if (encoding != Utility.CurrentEncoding)
            {
                previousEncoding = Utility.CurrentEncoding;
                Utility.CurrentEncoding = encoding;
            }

            try
            {
                if (processText) // replace italics, other actions if necessary
                {
                    foreach (TokenMsgDisp2 token in TokenList.Where(t => t is TokenMsgDisp2))
                    {
                        token.Message = ReplaceItalics(token.Message);
                        token.UpdateData();
                    }
                }

                byte[] output = Tokenizer.AssembleAsData(TokenList);

                if (output.Length > 0xFFFF)
                {
                    throw new ScriptFileException("Script length exceeded. Please split the script before saving it.");
                }

                saved = SaveFile(filename, output);
                if (saved)
                    ChangedFile = false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (previousEncoding != null) Utility.CurrentEncoding = previousEncoding;
            }

            return saved;
        }

        public bool ExportTextFile(string filename)
        {
            if (filename == "") return false;

            byte[] output = Tokenizer.AssembleAsText(Name, TokenList);

            return SaveFile(filename, output);
        }

        private bool SaveFile(string fileName, byte[] data)
        {
            new FileInfo(fileName).Directory.Create();

            var stream_out = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            stream_out.Write(data, 0, data.Length);
            stream_out.Close();
            return true;
        }

    }
}
