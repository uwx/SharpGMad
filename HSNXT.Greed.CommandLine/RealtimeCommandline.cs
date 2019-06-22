using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HSNXT.Greed
{
    /// <summary>
    /// Provides an interface for handling realtime functionality from the commandline.
    /// </summary>
    public static class RealtimeCommandline
    {
        /// <summary>
        /// The currently open addon.
        /// </summary>
        private static RealtimeAddon _addonHandle;

        /// <summary>
        /// The main method and entry point for command-line operation.
        /// </summary>
        public static int EntryPoint(string[] args)
        {
            // There might be a parameter specified.
            string strFile;
            try
            {
                var fileIndex = Array.FindIndex(args, a => a == "-file");

                if (fileIndex == -1)
                    throw new InvalidOperationException(); // This means that the switch does not exist

                strFile = args[fileIndex + 1];
            }
            catch (Exception)
            {
                strFile = "";
            }

            // Autoload addon from command line arguments
            if (!string.IsNullOrEmpty(strFile))
                LoadAddon(strFile);

            while (true)
            {
                if (_addonHandle == null)
                {
                    Console.Write("SharpGMad> ");
                }
                else if (_addonHandle != null)
                {
                    Console.Write("{0}{1}{2}{3}{4}",
                        Path.GetFileName(_addonHandle.AddonPath),
                        Whitelist.Override ? "!" : null,
                        _addonHandle.CanWrite ? null : " (read-only)",
                        _addonHandle.Modified ? "*" : null,
                        _addonHandle.Pullable ? "!" : null
                    );
                    Console.Write("> ");
                }

                var input = Console.ReadLine();
                var command = input.Split(' ');

                switch (command[0].ToLowerInvariant())
                {
                    case "new":
                        try
                        {
                            NewAddon(command[1]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The filename was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "load":
                        try
                        {
                            LoadAddon(command[1]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The filename was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "add":
                    case "fadd":
                        try
                        {
                            AddFile(command[1], command.Length == 3 ? command[2] : null, command[0][0] != 'f');
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The file path was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "addfolder":
                    case "faddfolder":
                        try
                        {
                            AddFolder(command[1], command[0][0] != 'f');
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The folder was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "list":
                        ListFiles();
                        break;
                    case "remove":
                        try
                        {
                            RemoveFile(command[1]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The filename was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "extract":
                        var extractPath = string.Empty;
                        try
                        {
                            extractPath = command[2];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Noop.
                        }

                        try
                        {
                            ExtractFile(command[1], extractPath);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The filename was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "mget":
                        string folder;
                        try
                        {
                            folder = command[1];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The folder was not specified.");
                            Console.ResetColor();
                            break;
                        }

                        if (!Directory.Exists(folder))
                        {
                            try
                            {
                                Directory.CreateDirectory(folder);
                            }
                            catch (IOException)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("There was a problem creating the output directory.");
                                Console.ResetColor();
                                break;
                            }
                        }

                        if (command.Length < 3)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The filename was not specified.");
                            Console.ResetColor();
                            break;
                        }

                        var eparam = new string[command.Length - 2];
                        try
                        {
                            for (var i = 2; i < command.Length; i++)
                            {
                                eparam[i - 2] = command[i];
                            }
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Noop.
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("There was an error parsing the filelist.");
                            Console.ResetColor();
                            break;
                        }

                        foreach (var path in eparam)
                        {
                            var outpath = folder + Path.DirectorySeparatorChar + Path.GetFileName(path);

                            ExtractFile(path, outpath);
                        }

                        break;
                    case "export":
                        if (!_addonHandle.CanWrite)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Addon read-only. Use `extract` to unpack files from it.");
                            Console.ResetColor();
                            break;
                        }

                        var exportPath = string.Empty;
                        try
                        {
                            exportPath = command[2];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Noop.
                        }

                        try
                        {
                            ExportFile(command[1], exportPath);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            if (_addonHandle.WatchedFiles.Count == 0)
                            {
                                Console.WriteLine("No files are exported.");
                            }
                            else
                            {

                                Console.WriteLine(_addonHandle.WatchedFiles.Count + " files currently exported:");
                                var i = 0;
                                foreach (var watch in _addonHandle.WatchedFiles)
                                {
                                    Console.WriteLine(++i +
                                        (watch.Modified ? "* " : " ") +
                                        watch.ContentPath + " at " + watch.FilePath);
                                }
                            }
                        }

                        break;
                    case "pull":
                        if (!_addonHandle.CanWrite)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Cannot modify a read-only addon.");
                            Console.ResetColor();
                            break;
                        }

                        try
                        {
                            PullFile(command[1]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            if (_addonHandle.WatchedFiles.Count == 0)
                            {
                                Console.WriteLine("No files are exported.");
                            }
                            else
                            {
                                Console.WriteLine("Pulling " + _addonHandle.WatchedFiles.Count + " files:");
                                foreach (var watch in _addonHandle.WatchedFiles)
                                {
                                    PullFile(watch.ContentPath);
                                }
                            }
                        }

                        break;
                    case "drop":
                        try
                        {
                            DropExport(command[1]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The filename was not specified.");
                            Console.ResetColor();
                        }

                        break;
                    case "get":
                        string parameter;
                        try
                        {
                            parameter = command[1];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The parameter was not specified.");
                            Console.ResetColor();
                            Console.WriteLine("The valid paramteres are: description tags title type");
                            break;
                        }

                        switch (parameter)
                        {
                            /*case "author":
                                Console.WriteLine(AddonHandle.OpenAddon.Author);
                                break;*/
                            case "description":
                                Console.WriteLine(_addonHandle.OpenAddon.Description);
                                break;
                            case "tags":
                                Console.WriteLine(string.Join(" ", _addonHandle.OpenAddon.Tags.ToArray()));
                                break;
                            case "title":
                                Console.WriteLine(_addonHandle.OpenAddon.Title);
                                break;
                            case "type":
                                Console.WriteLine(_addonHandle.OpenAddon.Type);
                                break;
                            default:
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine("The specified parameter is not valid.");
                                Console.ResetColor();
                                Console.WriteLine("The valid paramteres are: description tags title type");
                                break;
                        }

                        break;
                    case "set":
                        if (!_addonHandle.CanWrite)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Cannot modify a read-only addon.");
                            Console.ResetColor();
                            break;
                        }

                        string sparameter;
                        try
                        {
                            sparameter = command[1];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The parameter was not specified.");
                            Console.ResetColor();
                            Console.WriteLine("The valid paramteres are: description tags title type");
                            break;
                        }

                        string value;
                        try
                        {
                            var param = new string[command.Length - 2];
                            for (var i = 2; i < command.Length; i++)
                            {
                                param[i - 2] = command[i];
                            }

                            value = string.Join(" ", param);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Noop.
                            value = string.Empty;
                        }

                        switch (sparameter)
                        {
                            /*case "author":
                                SetAuthor(value);
                                break;*/
                            case "description":
                                SetDescription(value);
                                break;
                            case "tags":
                                SetTags(value.Split(' '));
                                break;
                            case "title":
                                SetTitle(value);
                                break;
                            case "type":
                                SetType(value);
                                break;
                            default:
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine("The specified parameter is not valid.");
                                Console.ResetColor();
                                Console.WriteLine("The valid paramteres are: author description tags title type");
                                break;
                        }

                        break;
                    case "close":
                    case "fclose":
                        CloseAddon(command[0][0] == 'f');
                        break;
                    case "push":
                        Push();
                        break;
                    case "shellexec":
                        if (_addonHandle == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("No addon is open.");
                            Console.ResetColor();
                            break;
                        }

                        try
                        {
                            ShellExecute(command[1]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The parameter was not specified.");
                            Console.ResetColor();
                        }
                        break;
                    case "path":
                        if (_addonHandle == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("No addon is open.");
                            Console.ResetColor();
                            break;
                        }

                        Console.WriteLine(_addonHandle.AddonPath);

                        break;
                    case "pwd":
                        Console.WriteLine(Directory.GetCurrentDirectory());
                        break;
                    case "cd":
                        try
                        {
                            string path;
                            try
                            {
                                var param = new string[command.Length - 1];
                                for (var i = 1; i < command.Length; i++)
                                {
                                    param[i - 1] = command[i];
                                }

                                path = string.Join(" ", param);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Noop.
                                path = string.Empty;
                            }

                            Directory.SetCurrentDirectory(path);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The folder was not specified.");
                            Console.ResetColor();
                        }
                        catch (IOException e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("There was a problem switching to that folder.");
                            Console.ResetColor();
                            Console.WriteLine(e.Message);
                        }
                        break;
                    case "ls":
                    case "dir":
                        try
                        {
                            var files = Directory.EnumerateFileSystemEntries(Directory.GetCurrentDirectory(),
                                "*", SearchOption.TopDirectoryOnly).ToArray();

                            if (files.Length == 0)
                            {
                                Console.WriteLine("0 files.");
                                break;
                            }

                            Console.WriteLine(files.Length + " files:");

                            foreach (var f in files)
                            {
                                FileSystemInfo fi = new FileInfo(f);

                                try
                                {
                                    Console.WriteLine("{0,10} {1,20} {2,30}", ((int)((FileInfo)fi).Length).HumanReadableSize(), fi.LastWriteTime.ToString(CultureInfo.InvariantCulture), fi.Name);
                                }
                                catch (FileNotFoundException)
                                {
                                    // Noop. The entry is a folder.
                                    fi = new DirectoryInfo(f);
                                    Console.WriteLine("{0,10} {1,20} {2,30}", "[DIR]", fi.LastWriteTime.ToString(CultureInfo.InvariantCulture), fi.Name);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("There was a problem listing the files.");
                            Console.ResetColor();
                            Console.WriteLine(e.Message);
                        }

                        break;
                    case "?":
                    case "help":
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("SharpGMad " + Shared.PrettyVersion);
                        Console.WriteLine("Available commands:");
                        Console.ResetColor();

                        if (_addonHandle == null)
                        {
                            Console.WriteLine("load <filename>            Loads <filename> addon into the memory");
                            if (!Whitelist.Override)
                                Console.WriteLine("new <filename>             Create a new, empty addon named <filename>");
                        }

                        if (_addonHandle != null)
                        {
                            if (_addonHandle.CanWrite && !Whitelist.Override)
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta; Console.Write("f"); Console.ResetColor();
                                Console.WriteLine("add <filename> [path]      Adds <filename> (to [path] if specified)");

                                Console.ForegroundColor = ConsoleColor.Magenta; Console.Write("f"); Console.ResetColor();
                                Console.WriteLine("addfolder <folder>         Adds all files from <folder> to the archive");
                            }
                            Console.WriteLine("list                       Lists the files in the memory");
                            if (_addonHandle.CanWrite && !Whitelist.Override)
                            {
                                Console.WriteLine("remove <filename>          Removes <filename> from the archive");
                            }
                            Console.WriteLine("extract <filename> [path]  Extract <filename> (to [path] if specified)");
                            Console.WriteLine("mget <folder> <f1> [f2...] Extract all specified files to <folder>");
                            if (_addonHandle.CanWrite && !Whitelist.Override)
                            {
                                Console.WriteLine("export                     View the list of exported files");
                                Console.WriteLine("export <filename> [path]   Export <filename> for editing (to [path] if specified)");
                                Console.WriteLine("pull                       Pull changes from all exported files");
                                Console.WriteLine("pull <filename>            Pull the changes of exported <filename>");
                                Console.WriteLine("drop <filename>            Drops the export for <filename>");
                            }
                            Console.WriteLine("get <parameter>            Prints the value of metadata <parameter>");
                            if (_addonHandle.CanWrite && !Whitelist.Override)
                            {
                                Console.WriteLine("set <parameter> [value]    Sets metadata <parameter> to the specified [value]");
                                Console.WriteLine("push                       Writes the changes to the disk");
                            }
                            Console.WriteLine("shellexec <path>           Execute the specified file");

                            Console.ForegroundColor = ConsoleColor.Magenta; Console.Write("f"); Console.ResetColor();
                            Console.WriteLine("close                      Closes the addon (dropping all changes)");

                            Console.WriteLine("path                       Prints the full path of the current addon.");
                        }

                        Console.WriteLine("pwd                        Prints SharpGMad's current working directory");
                        Console.WriteLine("cd <folder>                Changes the current working directory to <folder>");
#if MONO
                        Console.Write("ls                         ");
#endif
#if WINDOWS
                        Console.Write("dir                        ");
#endif
                        Console.WriteLine("List all files in the current directory");

                        Console.WriteLine("help                       Show the list of available commands");

                        if (_addonHandle == null)
                        {
                            Console.WriteLine("exit                       Exits");
                        }

                        if (_addonHandle != null)
                        {
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine("Commands marked with an f (for example: add, close) can be called as such (fadd, fclose).");
                            Console.WriteLine("Doing so will run a forced version of the command not prompting the user for error correction.");
                            Console.ResetColor();

                            if (_addonHandle.Modified)
                            {
                                Console.Write("The addon has been ");
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.Write("modified");
                                Console.ResetColor();
                                Console.WriteLine(". Execute `push` to save changes to the disk.");
                            }

                            if (_addonHandle.Pullable)
                            {
                                Console.Write("There are exported files which have been changed. ");
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("These changes are pullable.");
                                Console.ResetColor();
                                Console.WriteLine("`export` lists all exports or type `pull` to pull the changes.");
                            }
                        }

                        if (Whitelist.Override)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                            Console.ResetColor();
                        }

                        break;
                    case "exit":
                        if (_addonHandle != null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Cannot exit. An addon is open!");
                            Console.ResetColor();
                            break;
                        }

                        return 0;
                        //break;
                    default:
                        if (!string.IsNullOrWhiteSpace(command[0]))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Unknown operation.");
                            Console.ResetColor();
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Creates a new addon.
        /// </summary>
        /// <param name="filename">The filename where the addon should be saved to.</param>
        private static void NewAddon(string filename)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An addon is already open. Please close it first.");
                Console.ResetColor();
                return;
            }

            try
            {
                _addonHandle = RealtimeAddon.New(filename);
            }
            catch (UnauthorizedAccessException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("The file already exists. You might want to load it instead.");
                Console.ResetColor();
                return;
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There was a problem creating the file.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
                CloseAddon();
                return;
            }

            Console.WriteLine("Successfully created the new addon...");

            SetTitle();
            SetDescription();
            //SetAuthor(); // Author name setting is not yet implemented.
            SetType();
            SetTags();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Addon metadata setup finished.");
            Console.ResetColor();

            // Write initial content
            Push();
        }

        /// <summary>
        /// Sets the title of the addon.
        /// </summary>
        /// <param name="title">Optional. The new title the addon should have.</param>
        private static void SetTitle(string title = null)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (string.IsNullOrEmpty(title))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Title? ");
                Console.ResetColor();
                title = Console.ReadLine();
            }

            _addonHandle.OpenAddon.Title = title;
            _addonHandle.Modified = true;
        }

        /// <summary>
        /// Sets the description of the addon.
        /// </summary>
        /// <param name="description">Optional. The new description the addon should have.</param>
        private static void SetDescription(string description = null)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (string.IsNullOrEmpty(description))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Short description? ");
                Console.ResetColor();
                description = Console.ReadLine();
            }

            _addonHandle.OpenAddon.Description = description;
            _addonHandle.Modified = true;
        }

        /*/// <summary>
        /// Sets the author of the addon.
        /// </summary>
        /// <param name="author">Optional. The new author the addon should have.</param>
        private static void SetAuthor(string author = null)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (!AddonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (author == String.Empty || author == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Author? ");
                Console.ResetColor();
                Console.WriteLine("This currently has no effect as the addon is always written with \"Author Name\".");
                //author = Console.ReadLine();
                author = "Author Name";
            }

            AddonHandle.OpenAddon.Author = author;
            AddonHandle.Modified = true;
        }*/

        /// <summary>
        /// Sets the type of the addon.
        /// </summary>
        /// <param name="type">Optional. The new type the addon should have.</param>
        private static void SetType(string type = null)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (string.IsNullOrEmpty(type))
            {
                while (!Tags.TypeExists(type))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Type? ");
                    Console.ResetColor();
                    Console.Write("Please choose ONE from the following: ");
                    Console.WriteLine(string.Join(" ", Tags.Type));
                    type = Console.ReadLine();

                    if (!Tags.TypeExists(type))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The specified type is not valid.");
                        Console.ResetColor();
                    }
                }
            }
            else
            {
                if (!Tags.TypeExists(type))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The specified type is not valid.");
                    Console.ResetColor();
                    return;
                }
            }

            _addonHandle.OpenAddon.Type = type;
            _addonHandle.Modified = true;
        }

        /// <summary>
        /// Sets the tags of the addon.
        /// </summary>
        /// <param name="tagsInput">Optional. The new tags the addon should have.</param>
        private static void SetTags(string[] tagsInput = null)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            var tags = new List<string>(2);
            if (tagsInput == null || tagsInput.Length == 0 || tagsInput[0] == string.Empty)
            {
                var allTagsValid = false;
                while (!allTagsValid)
                {
                    tags.Clear();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Tags? ");
                    Console.ResetColor();
                    Console.Write("Please choose ZERO, ONE or TWO from the following: ");
                    Console.WriteLine(string.Join(" ", Tags.Misc));

                    tagsInput = Console.ReadLine().Split(' ');

                    allTagsValid = true;
                    if (tagsInput[0] != string.Empty)
                    {
                        // More than zero (one or two) elements: add the first one.
                        if (tagsInput.Length > 0)
                        {
                            if (!Tags.TagExists(tagsInput[0]))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("The specified tag \"" + tagsInput[0] + "\" is not valid.");
                                Console.ResetColor();
                                allTagsValid = false;
                                continue;
                            }

                            tags.Add(tagsInput[0]);
                        }

                        // More than one (two) elements: add the second one too.
                        if (tagsInput.Length > 1)
                        {
                            if (!Tags.TagExists(tagsInput[1]))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("The specified tag \"" + tagsInput[1] + "\" is not valid.");
                                Console.ResetColor();
                                allTagsValid = false;
                                continue;
                            }

                            tags.Add(tagsInput[1]);
                        }

                        if (tagsInput.Length > 2)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine("More than two tags specified. Only the first two is saved.");
                            Console.ResetColor();
                        }
                    }
                }
            }
            else
            {
                if (tagsInput[0] != string.Empty)
                {
                    // More than zero (one or two) elements: add the first one.
                    if (tagsInput.Length > 0)
                    {
                        if (!Tags.TagExists(tagsInput[0]))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The specified tag \"" + tagsInput[0] + "\" is not valid.");
                            Console.ResetColor();
                            return;
                        }

                        tags.Add(tagsInput[0]);
                    }

                    // More than one (two) elements: add the second one too.
                    if (tagsInput.Length > 1)
                    {
                        if (!Tags.TagExists(tagsInput[1]))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The specified tag \"" + tagsInput[1] + "\" is not valid.");
                            Console.ResetColor();
                            return;
                        }

                        tags.Add(tagsInput[1]);
                    }

                    if (tagsInput.Length > 2)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("More than two tags specified. Only the first two is saved.");
                        Console.ResetColor();
                    }
                }
            }

            _addonHandle.OpenAddon.Tags = tags;
            _addonHandle.Modified = true;
        }

        /// <summary>
        /// Loads an addon from the filesystem.
        /// </summary>
        /// <param name="filename">The path of the addon to load.</param>
        /// /// <param name="isOverrideReloading">Indicates whether the call is an override reload.</param>
        private static void LoadAddon(string filename, bool isOverrideReloading = false)
        {
            if (_addonHandle != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An addon is already open. Please close it first.");
                Console.ResetColor();
                return;
            }

            if (!isOverrideReloading) // If the current call is not an override call, reenable the whitelist
                Whitelist.Override = false;

            var shouldOverrideReload = false; // Whether a whitelist overriding reload should take place.

            Console.WriteLine("Loading file...");
            try
            {
                _addonHandle = RealtimeAddon.Load(filename, !FileExtensions.CanWrite(filename));
            }
            catch (WhitelistException e)
            {
                if (!Whitelist.Override)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("There was a problem opening the file.");
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("This addon is against the GMA whitelist rules defined by garry!\n" +
                        e.Message + "\n\nFor datamining purposes, it is still possible to open this addon, HOWEVER\n" +
                        "opening this addon is an illegal operation and SharpGMad will\nprevent further modifications.");
                    Console.ResetColor();

                    var decided = false;
                    while (!decided)
                    {
                        Console.Write("\nDo you want to enable opening this addon by overriding the whitelist? (y/n) ");
                        var decision = Console.ReadLine();
                        switch (decision)
                        {
                            case "n":
                            case "N":
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("The addon will not be opened.");
                                Console.ResetColor();
                                return;
                            case "y":
                            case "Y":
                                Whitelist.Override = true;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Whitelist overridden.");
                                Console.ResetColor();
                                shouldOverrideReload = true;

                                decided = true;
                                break;
                            default:
                                Console.WriteLine("Invalid input. Please write y for yes or n for no.");
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There was a problem opening the file.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
                CloseAddon();
                return;
            }

            // If a reloading is specified, do it and don't continue.
            if (shouldOverrideReload)
            {
                LoadAddon(filename, true);
                return;
            }

            foreach (var f in _addonHandle.OpenAddon.Files)
            {
                Console.WriteLine("\t" + f.Path + " [" + ((int)f.Size).HumanReadableSize() + "]");
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("File can not be written. Addon opened read-only.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Adds a file to the open addon.
        /// </summary>
        /// <param name="filename">The path of the file to be added.</param>
        /// <param name="internalPath">The path where the file should go within the addon.</param>
        /// <param name="promptPath">Whether the user should be asked if the internal path is not whitelisted.</param>
        private static void AddFile(string filename, string internalPath, bool promptPath = false)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            // Handle adding files without their full in-GMA path on the disk.
            // This way, users can just add a file from anywhere.
            // If the internal path counterpart is not found, they will be asked.

            var addPath = (string.IsNullOrWhiteSpace(internalPath) ? null : internalPath) ?? filename;

            if (promptPath)
            {
                if (!string.IsNullOrWhiteSpace(Whitelist.GetMatchingString(addPath)))
                    addPath = Whitelist.GetMatchingString(internalPath);
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("You tried to add " + filename +
                        (!string.IsNullOrWhiteSpace(internalPath) ? " (as " + internalPath + ")" : null) + ", but SharpGMad " +
                        "can't figure out where the file should be going inside the addon.\n" +
                        "Please specify the filename by hand (leave empty if wish to cancel): ");
                    Console.ResetColor();

                    var pathAsked = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(Whitelist.GetMatchingString(pathAsked)) && pathAsked.ToLowerInvariant() != "cancel")
                    {
                        if (!string.IsNullOrWhiteSpace(Whitelist.GetMatchingString(pathAsked)))
                            addPath = Whitelist.GetMatchingString(pathAsked);
                    }
                }
            }

            Console.WriteLine(filename + " as ");
            Console.Write("\t" + addPath);

            try
            {
                _addonHandle.AddFile(Whitelist.GetMatchingString(addPath), File.ReadAllBytes(filename));
            }
            catch (FileNotFoundException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n" + ex.Message);
                Console.ResetColor();
                return;
            }
            catch (IOException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nCannot add file. There was an error reading it.");
                Console.ResetColor();
                Console.WriteLine(ex.Message);
                return;
            }
            catch (IgnoredException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n\t\t[Ignored]");
                Console.ResetColor();
                return;
            }
            catch (WhitelistException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n\t\t[Not allowed by whitelist]");
                Console.ResetColor();
                return;
            }
            catch (ArgumentException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n\t\t[A file like this has already been added.]");
                Console.ResetColor();
                return;
            }

            Console.WriteLine(" [" + ((int)_addonHandle.GetFile(Whitelist.GetMatchingString(addPath)).Size).HumanReadableSize() + "]");
        }

        /// <summary>
        /// Adds all files from a specified folder to the addon.
        /// </summary>
        /// <param name="folder">The folder containing the files to be added.</param>
        /// <param name="promptPath">Whether the user should be asked to provide a path if a file can't be added automatically.</param>
        private static void AddFolder(string folder, bool promptPath = false)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (folder == string.Empty)
            {
                folder = Directory.GetCurrentDirectory();
            }

            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var file = f;
                file = file.Replace('\\', '/');

                AddFile(file, string.Empty, promptPath);
            }
        }

        /// <summary>
        /// Lists the files currently added to the addon.
        /// </summary>
        private static void ListFiles()
        {
            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine(_addonHandle.OpenAddon.Files.Count + " files in archive:");
            foreach (var file in _addonHandle.OpenAddon.Files)
            {
                Console.WriteLine(file.Path + " (" + ((int)file.Size).HumanReadableSize() + ")");
            }
        }

        /// <summary>
        /// Removes a file from the addon.
        /// </summary>
        /// <param name="filename">The path of the file to be removed.</param>
        private static void RemoveFile(string filename)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            try
            {
                _addonHandle.RemoveFile(filename);
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The file was not found in the archive.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Extract a file from the addon to a specified path on the local file system.
        /// Unlike ExportFile(), this does not set up a watch.
        /// </summary>
        /// <param name="filename">The path of the file in the addon to be exported.</param>
        /// <param name="extractPath">Optional. The path on the local file system where the file should be saved.
        /// If omitted, the file will be exported to the current working directory.</param>
        private static void ExtractFile(string filename, string extractPath = null)
        {
            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            try
            {
                _addonHandle.ExtractFile(filename, extractPath);
            }
            catch (FileNotFoundException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
                return;
            }
            catch (UnauthorizedAccessException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message + " Aborting extraction!");
                Console.ResetColor();
                return;
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There was a problem extracting the file.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("File extracted successfully.");
            Console.ResetColor();
        }

        /// <summary>
        /// Exports a file from the addon to a specified path on the local file system and sets up a watch.
        /// </summary>
        /// <param name="filename">The path of the file in the addon to be exported.</param>
        /// <param name="exportPath">Optional. The path on the local file system where the export should be saved.
        /// If omitted, the file will be exported to the current working directory.</param>
        private static void ExportFile(string filename, string exportPath = null)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            try
            {
                _addonHandle.ExportFile(filename, exportPath);
            }
            catch (FileNotFoundException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
                return;
            }
            catch (ArgumentException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message + " Aborting export!");
                Console.ResetColor();
                return;
            }
            catch (UnauthorizedAccessException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message + " Aborting export!");
                Console.ResetColor();
                return;
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There was a problem exporting the file.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
                return;
            }

            // Add a custom event handler so that the form gets updated when a file is pullable.
            _addonHandle.WatchedFiles.First(f => f.ContentPath == filename).FileChanged +=
                fsw_Changed;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("File exported successfully.");
            Console.ResetColor();
        }

        private static void fsw_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine("The exported file " +
                    _addonHandle.WatchedFiles.First(f => f.FilePath == e.FullPath).ContentPath + " changed!");
            }
            catch (InvalidOperationException)
            {
                // The export was removed earlier from the list.
                ((FileSystemWatcher)sender).Dispose();
            }
        }

        /// <summary>
        /// Removes a file export watcher and optionally deletes the exported file from the local file system.
        /// </summary>
        /// <param name="filename">The path of the file within the addon to be dropped.
        /// The exported path is known internally.</param>
        private static void DropExport(string filename)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            try
            {
                _addonHandle.DropExport(filename);
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The file is not in an exported state.");
                Console.ResetColor();
                return;
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Was unable to delete the exported file.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("Export dropped.");
        }

        /// <summary>
        /// Pulls the changes of the specified file from its exported version.
        /// </summary>
        /// <param name="filename">The internal path of the file changes should be pulled into.
        /// The exported path is known automatically.</param>
        private static void PullFile(string filename)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            try
            {
                _addonHandle.Pull(filename);
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The file is not in an exported state.");
                Console.ResetColor();
                return;
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to open exported file on disk.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine(filename + ": Successfully pulled the changes.");
        }

        /// <summary>
        /// Saves the changes of the addon to the disk.
        /// </summary>
        private static void Push()
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (!_addonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            foreach (var f in _addonHandle.OpenAddon.Files)
            {
                Console.WriteLine("File index: " + f.Path.TrimStart('/') + " [CRC: " + f.Crc + "] [Size:" + ((int)f.Size).HumanReadableSize() + "]");
            }

            try
            {
                _addonHandle.Save();
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There was an error saving the changes.");
                Console.ResetColor();
                Console.WriteLine(e.Message);
                return;
            }
            
            Console.WriteLine("Successfully saved the addon.");
        }

        /// <summary>
        /// Executes the specified file from the archive.
        /// </summary>
        /// <param name="path">The path of the file WITHIN the addon.</param>
        private static void ShellExecute(string path)
        {
            string temppath;
            try
            {
                temppath = Path.GetTempPath() + "/" + Path.GetFileName(path);

                try
                {
                    File.WriteAllBytes(temppath, _addonHandle.GetFile(path).Content);
                }
                catch (FileNotFoundException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The file was not found in the archive!");
                    Console.ResetColor();
                    return;
                }
                catch (Exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The file couldn't be saved to the disk.");
                    Console.ResetColor();
                    return;
                }
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The file was not found in the archive!");
                Console.ResetColor();
                return;
            }

            // Start the file
            Process.Start(new ProcessStartInfo(temppath)
            {
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Closes the currently open addon connection.
        /// </summary>
        private static void CloseAddon(bool forced = false)
        {
            if (_addonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (_addonHandle != null && !forced && _addonHandle.Modified)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The open addon has been modified!");
                Console.ResetColor();
                Console.WriteLine("Please save it (push) or use forced close (fclose) to drop the changes.");
                return;
            }

            _addonHandle.Close();
            _addonHandle = null;

            // Closing an addon reenables the whitelist.
            Whitelist.Override = false;
        }
    }
}