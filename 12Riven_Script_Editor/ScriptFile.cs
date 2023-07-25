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

        public bool ImportCSV(Stream reader)
        {
            // bad. horrible. temporary. but it works. -chroi
            int i = 0;

            List<Type> countedTokenTypes = new List<Type>() { typeof(TokenMsgDisp2), typeof(TokenSelectDisp2) };
            List<Token> countedTokens = TokenList.Where(l => countedTokenTypes.Contains(l.GetType())).ToList();

            Regex lineNumberRegex = new Regex(@"^\d+(?=\. )");
            Regex selectChoiceRegex = new Regex(@"([『“].*?[』”])\s*(/|$)");

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

        public bool SaveScriptFile(string filename)
        {
            if (filename == "") return false;

            byte[] output = Tokenizer.AssembleAsData(TokenList);

            if (output.Length > 0xFFFF)
            {
                throw new ScriptFileException("Script length exceeded. Please split the script before saving it.");
            }

            bool saved = SaveFile(filename, output);
            if (saved)
                ChangedFile = false;
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
            var stream_out = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            stream_out.Write(data, 0, data.Length);
            stream_out.Close();
            return true;
        }

    }
}
