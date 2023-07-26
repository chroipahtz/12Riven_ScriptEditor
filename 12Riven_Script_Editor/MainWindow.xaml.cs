using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;

using Riven_Script_Editor.Tokens;
using Riven_Script_Editor.FileTypes;
using System.Configuration;
using System.Text.RegularExpressions;
using Csv;
using System.Net.Http;
using System.Windows.Forms.VisualStyles;

namespace Riven_Script_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<ListViewItem> lvList = new ObservableCollection<ListViewItem>();
        string folder = "";
        string filename = "";
        string splittedFilenameEnding = "_continued";
        bool searchEndOfFile = false;
        readonly Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        public Grid Grid;
        public ListBox EntriesList;
        public TextBlock ScriptSizeTextBlock;

        public string currentFilename;
        public ScriptFile currentScript;
        public Dictionary<string, ScriptFile> ScriptFiles;

        public ScriptSizeNotifier scriptSizeNotifier;
        public ScriptListFileManager scriptListFileManager;

        // old convenience getters
        public List<Token> TokenList { get { return currentScript.TokenList; } }
        public ATokenizer Tokenizer { get { return currentScript.Tokenizer; } }
        public bool ChangedFile { get { return currentScript.ChangedFile; } set { currentScript.ChangedFile = value; } }

        public MainWindow()
        {
            InitializeComponent();

            scriptListFileManager = new ScriptListFileManager(GetConfig("list_file"));
            ScriptFile.scriptListFileManager = scriptListFileManager;

            ScriptFiles = new Dictionary<string, ScriptFile>();

            Grid = ((MainWindow)Application.Current.MainWindow).GuiArea;
            EntriesList = ((MainWindow)Application.Current.MainWindow).listviewEntries;
            ScriptSizeTextBlock = ((MainWindow)Application.Current.MainWindow).ScriptSizeCounter;

            listviewFiles.DataContext = scriptListFileManager;
            listviewFiles.SelectionChanged += ListViewFiles_SelectionChanged;

            this.Closing += MainWindow_Closing;

            textbox_inputFolder.Text = GetConfig("input_folder");
            textbox_inputFolderJp.Text = GetConfig("input_folder_jp");
            textbox_listFile.Text = GetConfig("list_file");
            textbox_exportedAfs.Text = GetConfig("exported_afs");
            checkbox_SearchCaseSensitive.IsChecked = GetConfig("case_sensitive") == "1";
            checkbox_SearchAllFiles.IsChecked = GetConfig("search_all_files") == "1";
            textbox_search.Text = GetConfig("last_search");

            MenuViewFolder.IsChecked = GetConfig("view_folders", "1") == "1";
            if (!(bool)MenuViewFolder.IsChecked)
                GridTextboxes.Visibility = Visibility.Collapsed;
            MenuViewDescription.IsChecked = GetConfig("view_description", "1") == "1";
            MenuViewLabel.IsChecked = GetConfig("view_label", "1") == "1";

            textbox_inputFolder.TextChanged += (sender, ev) => { UpdateConfig("input_folder", textbox_inputFolder.Text); BrowseInputFolder(null, null); };
            textbox_inputFolderJp.TextChanged += (sender, ev) => UpdateConfig("input_folder_jp", textbox_inputFolderJp.Text);
            textbox_listFile.TextChanged += (sender, ev) => { UpdateConfig("list_file", textbox_listFile.Text); LoadScriptList(textbox_listFile.Text); };
            textbox_exportedAfs.TextChanged += (sender, ev) => UpdateConfig("exported_afs", textbox_exportedAfs.Text);
            checkbox_SearchCaseSensitive.Checked += (sender, ev) => UpdateConfig("case_sensitive", "1");
            checkbox_SearchCaseSensitive.Unchecked += (sender, ev) => UpdateConfig("case_sensitive", "0");
            checkbox_SearchAllFiles.Checked += (sender, ev) => UpdateConfig("search_all_files", "1");
            checkbox_SearchAllFiles.Unchecked += (sender, ev) => UpdateConfig("search_all_files", "0");
            textbox_search.TextChanged += (sender, ev) => UpdateConfig("last_search", textbox_search.Text);
            textbox_search.KeyDown += Textbox_search_KeyDown;
            BrowseInputFolder(null, null);
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            bool success = CheckUnsavedChanges();
            e.Cancel = !success;
        }
        
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var x = new ExceptionPopup(e.Exception);
            x.ShowDialog();
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        private bool CheckUnsavedChanges(string filename = null) 
        {
            IEnumerable<ScriptFile> scriptFiles = null;

            // Check for file changes, then prompt user to save
            if (!string.IsNullOrEmpty(filename))
            {
                ScriptFile loadedFile = GetScriptFile(filename, false);
                if (loadedFile != null)
                    scriptFiles = new List<ScriptFile>() { loadedFile };
            }
            else    
                scriptFiles = ScriptFiles.Values;

            if (scriptFiles == null)
                return true;

            foreach (ScriptFile scriptFile in scriptFiles)
            {
                if (scriptFile.ChangedFile)
                {
                    MessageBoxResult dialogResult = MessageBox.Show($"{scriptFile.Name} changed. Save?", "Unsaved changes", MessageBoxButton.YesNoCancel);

                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        string path_en = System.IO.Path.Combine(folder, scriptFile.Name);

                        if (!scriptFile.SaveScriptFile(path_en)) return false;
                    }
                    else if (dialogResult == MessageBoxResult.Cancel)
                        return false;
                }
            }
            return true;
        }

        private void Textbox_search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SearchNext(null, null);
        }

        private string GetConfig(string key, string initial = "")
        {
            if (config.AppSettings.Settings.AllKeys.Contains(key))
                return config.AppSettings.Settings[key].Value;

            return initial;
        }

        private void UpdateConfig(string key, string value)
        {
            
            config.AppSettings.Settings.Remove(key);
            config.AppSettings.Settings.Add(key, value);
            config.Save();
        }

        private void BrowseInputFolder(object sender, RoutedEventArgs e)
        {
            if (sender == null)
            {
                folder = textbox_inputFolder.Text;
                return;
            }

            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                textbox_inputFolder.Text = dialog.FileName;
        }

        private void BrowseInputFolderJp(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                textbox_inputFolderJp.Text = dialog.FileName;
        }

        private void LoadScriptList(string filepath)
        {
            scriptListFileManager.Load(filepath);
        }

        private void BrowseFilelist(object sender, RoutedEventArgs e) {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textbox_listFile.Text = dialog.FileName;
            }
                

        }

        private void BrowseExportedAfs(object sender, RoutedEventArgs e) {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                textbox_exportedAfs.Text = dialog.FileName;
        }

        private void SearchFocus(object sender, RoutedEventArgs e)
        {
            textbox_search.Focus();
        }

        private void SearchNext(object sender, RoutedEventArgs e)
        {
            Search(true);
        }

        private void SearchPrev(object sender, RoutedEventArgs e)
        {
            Search(false);
        }

        private void Search(bool next)
        {
            if (textbox_search.Text == "")
                return;

            int mod(int k, int n) { return ((k %= n) < 0) ? k + n : k; }

            if ((bool)checkbox_SearchAllFiles.IsChecked)
            {
                int startIdx = listviewFiles.SelectedIndex;
                if (startIdx == -1) startIdx = 0;

                int idx = startIdx;

                while (!SearchToken(textbox_search.Text, next, (bool)checkbox_SearchCaseSensitive.IsChecked))
                {
                    //bool success = CheckUnsavedChanges();
                    //if (!success)
                    //    return;

                    if (next)
                        idx = mod(idx + 1, listviewFiles.Items.Count);
                    else
                        idx = mod(idx - 1, listviewFiles.Items.Count);
                    
                    if (idx == startIdx)
                    {
                        MessageBox.Show("Searched all files");
                        break;
                    }

                    // Select and focus on the file
                    listviewFiles.SelectedIndex = idx;
                    listviewFiles.UpdateLayout();
                    listviewFiles.ScrollIntoView(listviewFiles.Items[idx]);
                }
            }
            else
            {
                if (SearchToken(textbox_search.Text, next, (bool)checkbox_SearchCaseSensitive.IsChecked))
                    MessageBox.Show("End of file");
            }
        }

        public bool SearchToken(string text, bool next, bool case_sensitive)
        {
            int idx = TokenListView.SelectedIndex;
            if (idx == -1)
                idx = 0;

            //text = Utility.StringDoubleSpace(text);
            if (!case_sensitive)
                text = text.ToLower();

            if (searchEndOfFile)
            {
                if (next) idx = 0;
                else idx = TokenListView.Items.Count - 1;
                searchEndOfFile = false;
            }

            while (true)
            {
                if (next)
                {
                    idx++;
                    if (idx >= TokenListView.Items.Count - 1) break;
                }
                else
                {
                    idx--;
                    if (idx < 0) break;
                }

                object t = TokenListView.Items[idx];
                string msg = (t as Token).GetMessages();

                if (msg == null) continue;

                if (!case_sensitive)
                    msg = msg.ToLower();

                if (msg.Contains(text))
                {
                    // Select and focus on the token
                    TokenListView.SelectedIndex = idx;
                    TokenListView.UpdateLayout();
                    TokenListView.ScrollIntoView(TokenListView.Items[idx]);
                    return true;
                }
            }

            searchEndOfFile = true;
            //MessageBox.Show("End of file");
            return false;
        }

        private void FocusTextNext(object sender, RoutedEventArgs e)
        {
            
            int idx = TokenListView.SelectedIndex;
            if (idx == -1)
                idx = 0;

            while (true)
            {
                if (true)
                {
                    idx++;
                    if (idx >= TokenListView.Items.Count - 1) break;
                }
                else
                {
                    idx--;
                    if (idx < 0) break;
                }

                object t = TokenListView.Items[idx];
                string msg = (t as Token).GetMessages();

                if (msg != null)
                {
                    // Select and focus on the token
                    TokenListView.SelectedIndex = idx;
                    TokenListView.UpdateLayout();
                    TokenListView.ScrollIntoView(TokenListView.Items[idx]);

                    // Focus the message field
                    if (t is TokenMsgDisp2)
                        GetGridAtPos(4, 1).Focus();
                    //else if (t is TokenSystemMsg)
                    //    GetGridAtPos(3, 1).Focus();
                    else if (t is TokenSelectDisp2)
                        GetGridAtPos(3, 1).Focus();

                    return;
                }
            }

            MessageBox.Show("End of file. No more text.");
           
        }

        UIElement GetGridAtPos(int row, int col)
        {
            foreach (UIElement e in Grid.Children)
            {
                if (Grid.GetRow(e) == row && Grid.GetColumn(e) == col)
                    return e;
            }
            return null;
        }

        public bool Search(string text, bool next, bool case_sensitive)
        {
            int idx = TokenListView.SelectedIndex;
            if (idx == -1)
                idx = 0;

            //text = Utility.StringDoubleSpace(text);
            if (!case_sensitive)
                text = text.ToLower();

            if (searchEndOfFile)
            {
                if (next) idx = 0;
                else idx = TokenListView.Items.Count - 1;
                searchEndOfFile = false;
            }

            while (true)
            {
                if (next)
                {
                    idx++;
                    if (idx >= TokenListView.Items.Count - 1) break;
                }
                else
                {
                    idx--;
                    if (idx < 0) break;
                }

                object t = TokenListView.Items[idx];
                string msg = (t as Token).GetMessages();

                if (msg == null) continue;

                if (!case_sensitive)
                    msg = msg.ToLower();

                if (msg.Contains(text))
                {
                    // Select and focus on the token
                    TokenListView.SelectedIndex = idx;
                    TokenListView.UpdateLayout();
                    TokenListView.ScrollIntoView(TokenListView.Items[idx]);
                    return true;
                }
            }

            searchEndOfFile = true;
            //MessageBox.Show("End of file");
            return false;
        }


        private void MenuViewFolders_Clicked(object sender, RoutedEventArgs e)
        {
            MenuViewFolder.IsChecked = !MenuViewFolder.IsChecked;
            UpdateConfig("view_folders", MenuViewFolder.IsChecked ? "1": "0");
            GridTextboxes.Visibility = MenuViewFolder.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MenuViewDescription_Clicked(object sender, RoutedEventArgs e)
        {
            MenuViewDescription.IsChecked = !MenuViewDescription.IsChecked;
            UpdateConfig("view_description", MenuViewDescription.IsChecked ? "1" : "0");

            if (TokenListView.SelectedItem != null)
                (TokenListView.SelectedItem as Token).UpdateGui(this);
        }

        private void MenuViewLabel_Clicked(object sender, RoutedEventArgs e)
        {
            MenuViewLabel.IsChecked = !MenuViewLabel.IsChecked;
            UpdateConfig("view_label", MenuViewLabel.IsChecked ? "1" : "0");

            if (TokenListView.SelectedItem != null)
                (TokenListView.SelectedItem as Token).UpdateGui(this);
        }

        private void Menu_File_Save(object sender, RoutedEventArgs e)
        {
            SaveCurrentScriptFile();
        }

        private bool SaveCurrentScriptFile()
        {
            try
            {
                string path_en = System.IO.Path.Combine(folder, currentFilename);

                return currentScript.SaveScriptFile(path_en);
            }
            catch (ScriptFileException ex)
            {
                MessageBox.Show(ex.Message, "Script save failed");
                return false;
            }
        }

        private bool SaveFile(string fileName, byte[] data)
        {
            try
            {
                string outPath = System.IO.Path.Combine(folder, fileName);
                
                var stream_out = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite);
                stream_out.Write(data, 0, data.Length);
                stream_out.Close();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "File save failed");
                return false;
            }
        }

        private void Menu_Export_Afs(object sender, RoutedEventArgs e)
        {
            if (textbox_exportedAfs.Text == "") 
            {
                MessageBox.Show("Please select an AFS path.", "No AFS path selected");
                return;
            }

            if (textbox_exportedAfs.Text == "")
            {
                MessageBox.Show("Please select an AFS path.", "No AFS path selected");
                return;
            }

            IEnumerable<ScriptFile> changedFiles = ScriptFiles.Values.Where(f => f.ChangedFile);

            if (changedFiles.Count() > 0)
            {
                if (MessageBox.Show($"{changedFiles.Count()} scripts have changed and need to be saved before the AFS can be exported.\n\nSave all?", "Unsaved changes", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                    return;

                foreach (ScriptFile scriptFile in changedFiles)
                {
                    string path_en = System.IO.Path.Combine(folder, scriptFile.Name);

                    if (!scriptFile.SaveScriptFile(path_en)) {
                        MessageBox.Show($"Failed to save {scriptFile.Name}. Canceling AFS export.");
                        return;
                    }
                }
            }

            try
            {
                using (FileStream stream = new FileStream(textbox_exportedAfs.Text, FileMode.Create, FileAccess.Write))
                {
                    byte[] data = AFS.Pack(textbox_listFile.Text, textbox_inputFolder.Text);
                    stream.Write(data, 0, (int)data.Length);
                }
                MessageBox.Show("Exported " + textbox_exportedAfs.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error exporting AFS");
            }
        }

        private void Menu_Export_Txt(object sender, RoutedEventArgs e)
        {
            string fileName;
            try
            {
                fileName = (string)listviewFiles.SelectedItem;
                string outPath = System.IO.Path.Combine(folder, currentFilename);
                currentScript.ExportTextFile(outPath + ".txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Menu_Import_Csv(object sender, RoutedEventArgs e)
        {

            if (listviewFiles.SelectedItem == null)
            {
                MessageBox.Show("Please select a script file.", "No script file selected");
                return;
            }

            string csvPath = null;

            CommonOpenFileDialog dialog = new CommonOpenFileDialog("Import CSV");
            dialog.Filters.Add(new CommonFileDialogFilter("CSV File", "*.csv;*.txt"));
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                csvPath = dialog.FileName;
                currentScript.ImportCSV(new FileStream(csvPath, FileMode.Open, FileAccess.Read));
                if (TokenListView.SelectedItem != null)
                    (TokenListView.SelectedItem as Token).UpdateGui(this);
            }
            else
                return;
        }

		private async Task<bool> FetchCsv(string sheetId, string file) {
			var url = $"https://docs.google.com/spreadsheets/d/{sheetId}/gviz/tq?tqx=out:csv&sheet={file}";
            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetStreamAsync(url))
                    {
                        string path_en = System.IO.Path.Combine(folder, file);
                        string path_jp = System.IO.Path.Combine(textbox_inputFolderJp.Text, file);

                        ScriptFile script = GetScriptFile(file); 
                        script.ImportCSV(response);
                    }
                }
				return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error fetching CSV");
				return false;
            }
		}

        private async void Menu_Fetch_Csv(object sender, RoutedEventArgs e)
        {
            if (listviewFiles.SelectedItem == null)
            {
                MessageBox.Show("Please select a script file.", "No script file selected");
                return;
            }

            SheetIdDialog sheetIdDialog = new SheetIdDialog(GetConfig("spreadsheet_id"));
            if (sheetIdDialog.ShowDialog() == true)
            {
                UpdateConfig("spreadsheet_id", sheetIdDialog.SheetId);
				await FetchCsv(sheetIdDialog.SheetId, filename);
            }
        }

		private async void Menu_Fetch_Csv_Batch(object sender, RoutedEventArgs e)
        {
            SheetIdDialog sheetIdDialog = new SheetIdDialog(GetConfig("spreadsheet_id"));
            if (sheetIdDialog.ShowDialog() == true)
            {
                UpdateConfig("spreadsheet_id", sheetIdDialog.SheetId);

                int cnt = 0;
				foreach (string scriptName in scriptListFileManager.ScriptFilenameList)
                {
                    if (!await FetchCsv(sheetIdDialog.SheetId, scriptName))
                        break;
                    cnt++;
                }
                MessageBox.Show($"{cnt} / {scriptListFileManager.ScriptFilenameList.Count} sheets imported successfully.");
            }
        }

        private void Menu_Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TokenListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;
            var t = e.AddedItems[0];
            
            (t as Token).UpdateGui(this);
        }

        private void ListViewFiles_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (args.AddedItems.Count == 0)
                return;


            if (args.RemovedItems.Count > 0)
            {
                var success = CheckUnsavedChanges((string)args.RemovedItems[0]);
                if (!success)
                    return;
            }

            filename = (string)args.AddedItems[0];

            ChangeScriptFile(filename);
        }

        private ScriptFile GetScriptFile(string filename, bool load=true)
        {
            ScriptFile scriptFile = null;
            if (!ScriptFiles.TryGetValue(filename, out scriptFile) && load)
            {
                string path_en = System.IO.Path.Combine(folder, filename);
                string path_jp = System.IO.Path.Combine(textbox_inputFolderJp.Text, filename);
                scriptFile = ScriptFile.Load(path_en, path_jp);
                ScriptFiles[filename] = scriptFile;
            }
            return scriptFile;
        }

        private void ChangeScriptFile(string filename)
        {
            currentFilename = filename;
            currentScript = GetScriptFile(filename);

            ScriptSizeCounter.DataContext = new ScriptSizeNotifier(currentScript.TokenList);
            ((MainWindow)Application.Current.MainWindow).Title = "12R Script: " + filename;
            DataContext = new CommandViewBox(currentScript.TokenList.ToList());
        }

        private void TokenListView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContextMenu contextMenu = new ContextMenu();

            MenuItem menuItemSplit = new MenuItem();
            menuItemSplit.Header = "Split Script Here";
            string scriptName = (string)listviewFiles.SelectedItem;
            menuItemSplit.IsEnabled = !scriptName.Contains(splittedFilenameEnding) && (TokenList[TokenListView.SelectedIndex].Splitable != "No");
            menuItemSplit.Click += new RoutedEventHandler(ScriptSplitContextMenu_MouseUp);
            contextMenu.Items.Add(menuItemSplit);

            if (TokenList[TokenListView.SelectedIndex].OpCode == 0x0B)
            {
                MenuItem menuItemJoin = new MenuItem();
                menuItemJoin.Header = "Join Scripts";
                menuItemJoin.Click += new RoutedEventHandler(JoinTokens_MouseUp);
                contextMenu.Items.Add(menuItemJoin);
            }

            TokenListView.ContextMenu = contextMenu;
        }

        private void JoinTokens_MouseUp(Object sender, System.EventArgs e)
        {
            TokenExtGoto token = (TokenExtGoto)TokenList[TokenListView.SelectedIndex];
            FixExGotoIndexes(scriptListFileManager.getFilenameIndex(token.referencedFilename));
            byte[] binData = File.ReadAllBytes(System.IO.Path.Combine(folder, token.referencedFilename));
            scriptListFileManager.RemoveFilename(token.referencedFilename);
            ScriptTokenizer scriptTokenizer = new ScriptTokenizer(new DataWrapper(binData), scriptListFileManager);
            var breakoutTokenList = scriptTokenizer.ParseData();
            breakoutTokenList.RemoveAt(0); //remove header
            TokenList.RemoveAt(TokenList.Count - 1); //remove trailer
            TokenList.RemoveAt(TokenList.Count - 1); //remove end script opcode
            TokenList.RemoveAt(TokenList.Count - 1); //remove goto opcode
            TokenList.AddRange(breakoutTokenList);
            SaveFile((string)listviewFiles.SelectedItem, Tokenizer.AssembleAsData(TokenList));
            DataContext = new CommandViewBox(TokenList);
            ScriptSizeCounter.DataContext = new ScriptSizeNotifier(TokenList);
            File.Delete(folder + "\\" + token.referencedFilename);
        }

        private void ScriptSplitContextMenu_MouseUp(Object sender, System.EventArgs e) {
            string breakoutScriptName = (string)listviewFiles.SelectedItem;
            string[] scriptNameParts = breakoutScriptName.Split('.');
            breakoutScriptName = scriptNameParts[0] + splittedFilenameEnding + "." + scriptNameParts[1];
            byte scriptIndex = (byte)scriptListFileManager.AddFilename(breakoutScriptName);
            var breakoutTokenList = TokenList.GetRange(TokenListView.SelectedIndex + 1, TokenList.Count - TokenListView.SelectedIndex - 1);
            breakoutTokenList.Insert(0, TokenList[0]); //add copied header
            var commandBytes = new byte[] {0x0B, 0x06, scriptIndex, 0x00 , 0x00, 0x00};
            var callExtToken = new TokenExtGoto(null, commandBytes, 0, breakoutScriptName);
            TokenList.Insert(TokenListView.SelectedIndex + 1, callExtToken);
            TokenList.RemoveRange(TokenListView.SelectedIndex + 2, TokenList.Count() - TokenListView.SelectedIndex - 4);
            DataContext = new CommandViewBox(TokenList);
            ScriptSizeCounter.DataContext = new ScriptSizeNotifier(TokenList);
            SaveFile(breakoutScriptName, Tokenizer.AssembleAsData(breakoutTokenList));
            SaveFile((string)listviewFiles.SelectedItem, Tokenizer.AssembleAsData(TokenList));
        }

        private void FixExGotoIndexes(int removedIndex)
        {
            foreach (string filename in scriptListFileManager.ScriptFilenameList)
            {
                if (filename.Equals("DATA.BIN")) continue;
                string path_en = System.IO.Path.Combine(folder, filename);
                byte[] binData = File.ReadAllBytes(path_en);
                ScriptTokenizer tokenizer = new ScriptTokenizer(new DataWrapper(binData), scriptListFileManager);
                var tokenListTemp = tokenizer.ParseData();
                int index = tokenListTemp.FindIndex(token => token is TokenExtGoto);
                if (index >= 0 && tokenListTemp[index].ByteCommand[2] > removedIndex)
                {
                    tokenListTemp[index].ByteCommand[2]--;
                    SaveFile(filename, tokenizer.AssembleAsData(tokenListTemp));
                }
            }
            
        }
    }


    public class CommandViewBox : INotifyPropertyChanged
    {
        private ObservableCollection<Token> _commandList;
        public ObservableCollection<Token> CommandList
        {
            get => _commandList;
            set
            {
                _commandList = value;
                OnPropertyChanged(nameof(CommandList));
            }
        }

        public CommandViewBox(List<Token> tokenList)
        {
            CommandList = new ObservableCollection<Token>();

            foreach (var token in tokenList)
            {
                CommandList.Add(token);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ScriptSizeNotifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly List<Token> tokenList;

        private int _size;


        public ScriptSizeNotifier(List<Token> tokenList)
        {
            this.tokenList = tokenList;
            tokenList.ForEach(token => token.PropertyChanged += this.Token_PropertyChanged);
            Size = CalculateScriptSize();
        }


        private void OnPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }

        private void Token_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Size = CalculateScriptSize();
        }

        private int CalculateScriptSize()
        {
            int size = 0;
            tokenList.ForEach(token => size += token.Size);
            return size;
        }

        public int Size
        {
            get
            {
                return _size;
            }
            set
            {
                _size = value;
                OnPropertyChanged("Size");
            }
        }
    }
}