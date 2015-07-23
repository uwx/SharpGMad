﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpGMad
{
    abstract class Argument
    {
        public string Name { get; protected set; }
        protected object Value;
        protected string StringValue;
        public bool Mandatory { get; set; }
        protected Func<string, object> Projector;
        protected string BindErrorMessage;
        public Type ResultType { get; protected set; }
        protected Action<string> TypedSetValueDelegate;

        protected Argument()
        {
            this.Name = String.Empty;
            this.Mandatory = false;
            this.Projector = ((s) => { return s; });
            this.BindErrorMessage = String.Empty;
            this.ResultType = String.Empty.GetType();
            this.TypedSetValueDelegate = ((s) => this.BluntSetValue(s));

            Reset();
        }

        private void BluntSetValue(object value)
        {
            this.Value = value;
            this.StringValue = value.ToString();
        }

        public object GetValue()
        {
            return this.Value;
        }
        
        public void SetValue(string value)
        {
            this.TypedSetValueDelegate(value);
        }

        public void Reset()
        {
            this.Value = null;
            this.StringValue = String.Empty;
        }
    }

    class Argument<T> : Argument
    {
        private Func<string, T> TypedProjector;

        public Argument(string name, Func<string, T> proj, string bindErrMsg = "") : base()
        {
            base.Name = name;
            this.TypedProjector = proj;
            base.ResultType = proj.Method.ReturnType;
            base.BindErrorMessage = bindErrMsg;
            base.TypedSetValueDelegate = ((s) => this.TypedSetValue(s));
        }

        public Argument(string name, string value, Func<string, T> proj, string bindErrMsg = "") : this(name, proj, bindErrMsg)
        {
            if (proj == null)
                throw new ArgumentNullException("proj", "Projection delegate must be set. " +
                    "If you intend a string argument, use the base type Argument.");
            try
            {
                base.Value = proj(value);
            }
            catch (Exception e)
            {
                throw new ArgumentException(bindErrMsg, "value", e);
            }

            base.StringValue = value;
        }

        public Argument(string name, T value)
        {
            base.Name = name;
            base.Value = value;
            base.StringValue = value.ToString();
            base.ResultType = value.GetType();
        }

        public void TypedSetValue(string value)
        {
            try
            {
                this.Value = this.TypedProjector(value);
            }
            catch (Exception e)
            {
                throw new ArgumentException(this.BindErrorMessage, e);
            }

            this.StringValue = value;
        }
    }

    // TODO: support for accessibility
    [Flags]
    enum CommandAvailability : byte
    {
        Default = 0,

    }

    class Command
    {
        public string Verb { get; private set; }
        public string Description;
        public List<Argument> Arguments;
        public Dictionary<string, Argument> ArgumentsByName { get { return Arguments.ToDictionary(a => a.Name); } }
        private Action<Command> Dispatch;

        protected Command()
        {
            this.Arguments = new List<Argument>();
        }

        public Command(string verb, Action<Command> dispatch) : this()
        {
            this.Verb = verb;
            this.Dispatch = dispatch;
        }

        public void Invoke(string[] args)
        {
            RealtimeCommandline.WriteColor("Invoking command " + this.Verb +
                " with command-line arguments \"" + String.Join(";", args) + "\".", ConsoleColor.Yellow, true);
            // Bind all shell arguments to their values
            int i = 0;
            foreach (Argument arg in this.Arguments)
            {
                string stringArg = String.Empty;
                try
                {
                    stringArg = args[i++];
                }
                catch (IndexOutOfRangeException)
                {
                    if (arg.Mandatory)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("missing argument " + arg.Name);
                        Console.ResetColor();
                        Console.Write("Usage: " + this.Verb + " ");
                        foreach (Argument _arg in this.Arguments)
                            Console.Write((_arg.Mandatory ? "<" : "[") + _arg.Name + (_arg.Mandatory ? ">" : "]") + " ");
                        Console.WriteLine();

                        return; // Don't parse further
                    }
                }

                try
                {
                    arg.SetValue(stringArg);
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("invalid value for " + arg.Name);
                    Console.ResetColor();

                    if (String.IsNullOrWhiteSpace(e.Message) && e.InnerException != null 
                        && !String.IsNullOrWhiteSpace(e.InnerException.Message))
                        Console.WriteLine(" " + e.InnerException.Message);
                    else
                        Console.WriteLine(" " + e.Message);

                    return;
                }
            }

            // Call the associated method
            this.Dispatch(this);

            // Reset the arguments
            foreach (Argument arg in this.Arguments)
                arg.Reset();
        }
    }

    /// <summary>
    /// Provides an interface for handling realtime functionality from the commandline.
    /// </summary>
    static class RealtimeCommandline
    {
        /// <summary>
        /// The currently open addon.
        /// </summary>
        static RealtimeAddon AddonHandle;

        private static List<Command> Commands;

        /// <summary>
        /// The shell's current working directory within the open addon
        /// </summary>
        private static string CurrentWorkingDirectory;

        static RealtimeCommandline()
        {
            // Register the commands available in this shell
            Commands = new List<Command>();

            Command comm;

            // Create a new addon
            comm = new Command("new", (self) => {
                string filename = (string)self.ArgumentsByName["filename"].GetValue();
                NewAddon(filename);
            });
            comm.Description = "Create a new, empty addon named <filename>";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            Commands.Add(comm);

            // Load an addon
            comm = new Command("load", (self) =>
            {
                string filename = (string)self.ArgumentsByName["filename"].GetValue();
                LoadAddon(filename);
            });
            comm.Description = "Loads <filename> addon into the memory";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            Commands.Add(comm);

            // Add a file to the open addon
            comm = new Command("add", (self) => {
                string filename = (string)self.ArgumentsByName["filename"].GetValue();
                string path = (string)self.ArgumentsByName["path"].GetValue();
                AddFile(filename, path, true); // TODO: true becomes false if forced add
            });
            comm.Description = "Adds <filename> (as [path] if specified)";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            comm.Arguments.Add(new Argument<string>("path", (s) => { return s; }));
            Commands.Add(comm);

            // Add a whole folder
            comm = new Command("addfolder", (self) =>
            {
                string folder = (string)self.ArgumentsByName["folder"].GetValue();
                AddFolder(folder, true); // TODO: true becomes false if forced add
            });
            comm.Description = "Adds all files from <folder> to the archive";
            comm.Arguments.Add(new Argument<string>("folder",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The folder was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            Commands.Add(comm);

            // List files
            comm = new Command("list", (self) => { ListFiles(); });
            comm.Description = "Lists the files in the memory";
            Commands.Add(comm);

            // Remove a file
            comm = new Command("remove", (self) =>
            {
                string filename = (string)self.ArgumentsByName["filename"].GetValue();
                RemoveFile(filename);
            });
            comm.Description = "Removes <filename> from the archive";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            Commands.Add(comm);

            // Extract
            comm = new Command("extract", (self) =>
            {
                string filename = (string)self.ArgumentsByName["filename"].GetValue();
                string path = (string)self.ArgumentsByName["path"].GetValue();
                ExtractFile(filename, path);
            });
            comm.Description = "Extract <filename> (to [path] if specified)";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            comm.Arguments.Add(new Argument<string>("path", (s) => { return s; }));
            Commands.Add(comm);

            // Extract multiple files
            // TODO: support infinite number of arguments (like params)

            // Export
            // TODO: support overloading ...

            // Drop an export
            comm = new Command("drop", (self) =>
            {
                string filename = (string)self.ArgumentsByName["filename"].GetValue();
                DropExport(filename);
            });
            comm.Description = "Drops the export for <filename>";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            Commands.Add(comm);

            // Get a metadata
            // Set a metadata

            // Write changes to the disk
            comm = new Command("push", (self) => { Push(); });
            comm.Description = "Writes the changes to the disk";
            Commands.Add(comm);

            // Shell execute a file
            comm = new Command("shellexec", (self) =>
            {
                string path = (string)self.ArgumentsByName["filename"].GetValue();
                ShellExecute(path);
            });
            comm.Description = "Execute the specified <filename>";
            comm.Arguments.Add(new Argument<string>("filename",
                (s) =>
                {
                    if (String.IsNullOrWhiteSpace(s))
                        throw new ArgumentException("The filename was not specified.");
                    return s;
                })
                { Mandatory = true }
            );
            Commands.Add(comm);

            // Close the addon
            comm = new Command("close", (self) => { CloseAddon(false); }); // TODO: false becomes true if forced
            comm.Description = "Closes the addon (dropping all changes)";
            Commands.Add(comm);

            // Path of the open addon
            comm = new Command("path", (self) => {
                if (AddonHandle == null)
                    WriteColor("No addon is open.", ConsoleColor.Red, true); // TODO: support for accessibility
                else
                    Console.WriteLine(AddonHandle.AddonPath);
            });
            comm.Description = "Prints the full path of the current addon";
            Commands.Add(comm);

            // pwd
            comm = new Command("pwd", (self) => { Console.WriteLine(Directory.GetCurrentDirectory()); });
            comm.Description = "Prints SharpGMad's current working directory";
            Commands.Add(comm);

            // cd

            // List files in the working directory (outside)
            Action<Command> ls_dir_action = (self) =>
            {
                try
                {
                    IEnumerable<string> files = Directory.EnumerateFileSystemEntries(Directory.GetCurrentDirectory(),
                        "*", SearchOption.TopDirectoryOnly);

                    if (files.Count() == 0)
                    {
                        Console.WriteLine("0 files.");
                        return;
                    }
                    else
                        Console.WriteLine(files.Count() + " files:");

                    foreach (string f in files)
                    {
                        FileSystemInfo fi;
                        fi = new FileInfo(f);

                        try
                        {
                            Console.WriteLine(
                                String.Format("{0,10} {1,20} {2,30}", ((int)((FileInfo)fi).Length).HumanReadableSize(),
                                fi.LastWriteTime.ToString(), fi.Name)
                            );
                        }
                        catch (FileNotFoundException)
                        {
                            // Noop. The entry is a folder.
                            fi = new DirectoryInfo(f);
                            Console.WriteLine(
                                String.Format("{0,10} {1,20} {2,30}", "[DIR]",
                                fi.LastWriteTime.ToString(), fi.Name)
                            );
                            continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("There was a problem listing the files.");
                    Console.ResetColor();
                    Console.WriteLine(e.Message);
                    return;
                }
            };
#if MONO
            comm = new Command("ls", ls_dir_action);
#endif
#if WINDOWS
            comm = new Command("dir", ls_dir_action);
#endif
            comm.Description = "List all files in the current directory";
            Commands.Add(comm);

            // Switch to GUI mode
            comm = new Command("gui", (self) =>
            {
                if (AddonHandle == null)
                    System.Windows.Forms.Application.Run(new Main(new string[] { }, false));
                else if (AddonHandle is RealtimeAddon)
                {
                    if (AddonHandle.Modified)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Your addon is modified.");
                        Console.ResetColor();
                        Console.WriteLine("The GUI cannot be opened until the addon is saved.");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("The addon will close in the console and will be reopened by the GUI.");
                        // Save the addon path and close it in the console.
                        string addonpath = AddonHandle.AddonPath;

                        // Save whether the addon required a whitelist override.
                        bool whitelistOverride = Whitelist.Override;
                        CloseAddon(false);

                        // Reenable the override, if it was enabled. (CloseAddon() automatically disables it.)
                        Whitelist.Override = whitelistOverride;

                        // Open the GUI with the path. The addon will automatically reload.
                        System.Windows.Forms.Application.Run(new Main(new string[] { addonpath }, whitelistOverride));

                        // Set the override status again. (Closing the form disables the override again.)
                        Whitelist.Override = whitelistOverride;

                        // This thread will hang until the GUI is closed.
                        Console.WriteLine("GUI was closed. Reopening the addon...");
                        LoadAddon(addonpath, whitelistOverride);
                    }
                }
            });
            comm.Description = "Load the GUI";
            Commands.Add(comm);

            // Print available commands
            Action<Command> help_action = (self) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SharpGMad " + VersionExtensions.Pretty());
                Console.WriteLine("Available commands:");
                Console.ResetColor();

                if (AddonHandle == null)
                {
                    Console.WriteLine("load <filename>            Loads <filename> addon into the memory");
                    if (!Whitelist.Override)
                        Console.WriteLine("new <filename>             Create a new, empty addon named <filename>");
                }

                if (AddonHandle is RealtimeAddon)
                {
                    if (AddonHandle.CanWrite && !Whitelist.Override)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta; Console.Write("f"); Console.ResetColor();
                        Console.WriteLine("add <filename> [path]      Adds <filename> (to [path] if specified)");

                        Console.ForegroundColor = ConsoleColor.Magenta; Console.Write("f"); Console.ResetColor();
                        Console.WriteLine("addfolder <folder>         Adds all files from <folder> to the archive");
                    }
                    Console.WriteLine("list                       Lists the files in the memory");
                    if (AddonHandle.CanWrite && !Whitelist.Override)
                        Console.WriteLine("remove <filename>          Removes <filename> from the archive");
                    Console.WriteLine("extract <filename> [path]  Extract <filename> (to [path] if specified)");
                    Console.WriteLine("mget <folder> <f1> [f2...] Extract all specified files to <folder>");
                    if (AddonHandle.CanWrite && !Whitelist.Override)
                    {
                        Console.WriteLine("export                     View the list of exported files");
                        Console.WriteLine("export <filename> [path]   Export <filename> for editing (to [path] if specified)");
                        Console.WriteLine("pull                       Pull changes from all exported files");
                        Console.WriteLine("pull <filename>            Pull the changes of exported <filename>");
                        Console.WriteLine("drop <filename>            Drops the export for <filename>");
                    }
                    Console.WriteLine("get <parameter>            Prints the value of metadata <parameter>");
                    if (AddonHandle.CanWrite && !Whitelist.Override)
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

                if (AddonHandle == null || (AddonHandle is RealtimeAddon && !AddonHandle.Modified))
                    Console.WriteLine("gui                        Load the GUI");

                Console.WriteLine("help                       Show the list of available commands");

                if (AddonHandle == null)
                    Console.WriteLine("exit                       Exits");

                if (AddonHandle is RealtimeAddon)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("Commands marked with an f (for example: add, close) can be called as such (fadd, fclose).");
                    Console.WriteLine("Doing so will run a forced version of the command not prompting the user for error correction.");
                    Console.ResetColor();

                    if (AddonHandle.Modified)
                    {
                        Console.Write("The addon has been ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("modified");
                        Console.ResetColor();
                        Console.WriteLine(". Execute `push` to save changes to the disk.");
                    }

                    if (AddonHandle.Pullable)
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
            };
            comm = new Command("help", help_action);
            comm.Description = "Show the list of available commands";
            Commands.Add(comm);
            
            // TODO: support aliasing
            comm = new Command("?", help_action);
            comm.Description = "Show the list of available commands";
            Commands.Add(comm);

            // Exit
        }

        public static void WriteColor(object value, ConsoleColor color = default(ConsoleColor), bool newLine = true)
        {
            Console.ForegroundColor = color;
            Console.Write(value);
            if (newLine)
                Console.WriteLine();
            Console.ResetColor();
        }

        /// <summary>
        /// Entry point for realtime command-line
        /// </summary>
        public static int Main(string[] args)
        {
            // There might be a parameter specified.
            string strFile;
            try
            {
                int fileIndex = Array.FindIndex(args, a => a == "-file");

                if (fileIndex == -1)
                    throw new Exception(); // This means that the switch does not exist
                else
                    strFile = args[Array.FindIndex(args, a => a == "-file") + 1];
            }
            catch (Exception)
            {
                strFile = "";
            }

            // Autoload addon from command line arguments
            if (strFile != String.Empty && strFile != null)
                LoadAddon(strFile);

            while (true)
            {
                if (AddonHandle == null)
#if WINDOWS
                    Console.Write("SharpGMad> ");
#endif
#if MONO
                    Console.Write("$ ");
#endif
                else if (AddonHandle is RealtimeAddon)
                {
                    Console.Write(Path.GetFileName(AddonHandle.AddonPath) + (Whitelist.Override ? "!" : null) +
                        (AddonHandle.CanWrite ? null : " (read-only)") +
                        (AddonHandle.Modified ? "*" : null) + (AddonHandle.Pullable ? "!" : null));
#if WINDOWS
                     Console.Write("> ");
#endif
#if MONO
                    Console.Write("$ ");
#endif
                }

                string input = Console.ReadLine();
                string[] command = input.Split(' ');

                if (String.IsNullOrWhiteSpace(command[0]))
                    continue;

                IEnumerable<Command> com = Commands.Where(c => c.Verb == command[0]);
                if (com.Count() == 0)
                {
                    WriteColor("Unknown command", ConsoleColor.Red);
                }
                else
                {
                    com.First().Invoke(command.Skip(1).ToArray());
                    continue;
                }

                WriteColor("Attempting from the known legacy commands...", ConsoleColor.Yellow, true);
                switch (command[0].ToLowerInvariant())
                {
                    // TODO: make this a Command
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

                        string[] eparam = new string[command.Length - 2];
                        try
                        {
                            for (int i = 2; i < command.Length; i++)
                                eparam[i - 2] = command[i];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Noop.
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("There was an error parsing the filelist.");
                            Console.ResetColor();
                            break;
                        }

                        foreach (string path in eparam)
                        {
                            string outpath = folder + Path.DirectorySeparatorChar + Path.GetFileName(path);

                            ExtractFile(path, outpath);
                        }

                        break;
                    case "export":
                        if (!AddonHandle.CanWrite)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Addon read-only. Use `extract` to unpack files from it.");
                            Console.ResetColor();
                            break;
                        }

                        string exportPath = String.Empty;
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
                            if (AddonHandle.WatchedFiles.Count == 0)
                                Console.WriteLine("No files are exported.");
                            else
                            {

                                Console.WriteLine(AddonHandle.WatchedFiles.Count + " files currently exported:");
                                int i = 0;
                                foreach (FileWatch watch in AddonHandle.WatchedFiles)
                                    Console.WriteLine(++i +
                                        ((watch.Modified) ? "* " : " ") +
                                        watch.ContentPath + " at " + watch.FilePath);
                            }
                        }

                        break;
                    case "pull":
                        if (!AddonHandle.CanWrite)
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
                            if (AddonHandle.WatchedFiles.Count == 0)
                                Console.WriteLine("No files are exported.");
                            else
                            {
                                Console.WriteLine("Pulling " + AddonHandle.WatchedFiles.Count + " files:");
                                foreach (FileWatch watch in AddonHandle.WatchedFiles)
                                    PullFile(watch.ContentPath);
                            }
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
                                Console.WriteLine(AddonHandle.OpenAddon.Description);
                                break;
                            case "tags":
                                Console.WriteLine(String.Join(" ", AddonHandle.OpenAddon.Tags.ToArray()));
                                break;
                            case "title":
                                Console.WriteLine(AddonHandle.OpenAddon.Title);
                                break;
                            case "type":
                                Console.WriteLine(AddonHandle.OpenAddon.Type);
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
                        if (!AddonHandle.CanWrite)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Cannot modify a read-only addon.");
                            Console.ResetColor();
                            break;
                        }

                        string Sparameter;
                        try
                        {
                            Sparameter = command[1];
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
                            string[] param = new string[command.Length - 2];
                            for (int i = 2; i < command.Length; i++)
                            {
                                param[i - 2] = command[i];
                            }

                            value = String.Join(" ", param);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Noop.
                            value = String.Empty;
                        }

                        switch (Sparameter)
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
                    case "cd":
                        try
                        {
                            string path;
                            try
                            {
                                string[] param = new string[command.Length - 1];
                                for (int i = 1; i < command.Length; i++)
                                    param[i - 1] = command[i];

                                path = String.Join(" ", param);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Noop.
                                path = String.Empty;
                            }

                            Directory.SetCurrentDirectory(path);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("The folder was not specified.");
                            Console.ResetColor();
                            break;
                        }
                        catch (IOException e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("There was a problem switching to that folder.");
                            Console.ResetColor();
                            Console.WriteLine(e.Message);
                            break;
                        }
                        break;
                    case "exit":
                        if (AddonHandle is RealtimeAddon)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Cannot exit. An addon is open!");
                            Console.ResetColor();
                            break;
                        }

                        return 0;
                        //break;
                    default:
                        if (!String.IsNullOrWhiteSpace(command[0]))
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
        static void NewAddon(string filename)
        {
            if (Whitelist.Override)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Due to opening a whitelist non compliant addon, the restrictions and addon modification capability has been disabled.");
                Console.ResetColor();
                return;
            }

            if (AddonHandle is RealtimeAddon)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An addon is already open. Please close it first.");
                Console.ResetColor();
                return;
            }

            try
            {
                AddonHandle = RealtimeAddon.New(filename);
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

            if (!AddonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (title == String.Empty || title == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Title? ");
                Console.ResetColor();
                title = Console.ReadLine();
            }

            AddonHandle.OpenAddon.Title = title;
            AddonHandle.Modified = true;
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

            if (!AddonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (description == String.Empty || description == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Short description? ");
                Console.ResetColor();
                description = Console.ReadLine();
            }

            AddonHandle.OpenAddon.Description = description;
            AddonHandle.Modified = true;
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

            if (!AddonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (type == String.Empty || type == null)
            {
                while (!Tags.TypeExists(type))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Type? ");
                    Console.ResetColor();
                    Console.Write("Please choose ONE from the following: ");
                    Console.WriteLine(String.Join(" ", Tags.Type));
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

            AddonHandle.OpenAddon.Type = type;
            AddonHandle.Modified = true;
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

            if (!AddonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            List<string> tags = new List<string>(2);
            if (tagsInput == null || tagsInput.Length == 0 || tagsInput[0] == String.Empty)
            {
                bool allTagsValid = false;
                while (!allTagsValid)
                {
                    tags.Clear();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Tags? ");
                    Console.ResetColor();
                    Console.Write("Please choose ZERO, ONE or TWO from the following: ");
                    Console.WriteLine(String.Join(" ", Tags.Misc));

                    tagsInput = Console.ReadLine().Split(' ');

                    allTagsValid = true;
                    if (tagsInput[0] != String.Empty)
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
                            else
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
                            else
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
                if (tagsInput[0] != String.Empty)
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
                        else
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
                        else
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

            AddonHandle.OpenAddon.Tags = tags;
            AddonHandle.Modified = true;
        }

        /// <summary>
        /// Loads an addon from the filesystem.
        /// </summary>
        /// <param name="filename">The path of the addon to load.</param>
        /// /// <param name="isOverrideReloading">Indicates whether the call is an override reload.</param>
        private static void LoadAddon(string filename, bool isOverrideReloading = false)
        {
            if (AddonHandle is RealtimeAddon)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An addon is already open. Please close it first.");
                Console.ResetColor();
                return;
            }

            if (!isOverrideReloading) // If the current call is not an override call, reenable the whitelist
                Whitelist.Override = false;

            bool shouldOverrideReload = false; // Whether a whitelist overriding reload should take place.

            Console.WriteLine("Loading file...");
            try
            {
                AddonHandle = RealtimeAddon.Load(filename, !FileExtensions.CanWrite(filename));
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

                    bool decided = false;
                    string decision;
                    while (!decided)
                    {
                        Console.Write("\nDo you want to enable opening this addon by overriding the whitelist? (y/n) ");
                        decision = Console.ReadLine();
                        if (decision == "n" || decision == "N")
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("The addon will not be opened.");
                            Console.ResetColor();
                            decided = true;
                            return;
                        }
                        else if (decision == "y" || decision == "Y")
                        {
                            Whitelist.Override = true;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Whitelist overridden.");
                            Console.ResetColor();
                            shouldOverrideReload = true;

                            decided = true;
                        }
                        else
                        {
                            decided = false;
                            Console.WriteLine("Invalid input. Please write y for yes or n for no.");
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
                LoadAddon(filename, shouldOverrideReload);
                return;
            }

            foreach (ContentFile f in AddonHandle.OpenAddon.Files)
            {
                Console.WriteLine("\t" + f.Path + " [" + ((int)f.Size).HumanReadableSize() + "]");
            }

            if (!AddonHandle.CanWrite)
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

            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
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

            // Handle adding files without their full in-GMA path on the disk.
            // This way, users can just add a file from anywhere.
            // If the internal path counterpart is not found, they will be asked.

            string addPath = (String.IsNullOrWhiteSpace(internalPath) ? null : internalPath) ?? filename;

            if (promptPath)
            {
                if (!String.IsNullOrWhiteSpace(Whitelist.GetMatchingString(addPath)))
                    addPath = Whitelist.GetMatchingString(internalPath);
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("You tried to add " + filename +
                        (!String.IsNullOrWhiteSpace(internalPath) ? " (as " + internalPath + ")" : null) + ", but SharpGMad " +
                        "can't figure out where the file should be going inside the addon.\n" +
                        "Please specify the filename by hand (leave empty if wish to cancel): ");
                    Console.ResetColor();

                    string pathAsked = Console.ReadLine();
                    if (!String.IsNullOrWhiteSpace(Whitelist.GetMatchingString(pathAsked)) && pathAsked.ToLowerInvariant() != "cancel")
                    {
                        if (!String.IsNullOrWhiteSpace(Whitelist.GetMatchingString(pathAsked)))
                            addPath = Whitelist.GetMatchingString(pathAsked);
                    }
                }
            }

            Console.WriteLine(filename + " as ");
            Console.Write("\t" + addPath);

            try
            {
                AddonHandle.AddFile(Whitelist.GetMatchingString(addPath), File.ReadAllBytes(filename));
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

            Console.WriteLine(" [" + ((int)AddonHandle.GetFile(Whitelist.GetMatchingString(addPath)).Size).HumanReadableSize() + "]");
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

            if (!AddonHandle.CanWrite)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot modify a read-only addon.");
                Console.ResetColor();
                return;
            }

            if (folder == String.Empty)
                folder = Directory.GetCurrentDirectory();

            foreach (string f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string file = f;
                file = file.Replace('\\', '/');

                AddFile(file, String.Empty, promptPath);
            }
        }

        /// <summary>
        /// Lists the files currently added to the addon.
        /// </summary>
        private static void ListFiles()
        {
            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine(AddonHandle.OpenAddon.Files.Count + " files in archive:");
            foreach (ContentFile file in AddonHandle.OpenAddon.Files)
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

            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
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

            try
            {
                AddonHandle.RemoveFile(filename);
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The file was not found in the archive.");
                Console.ResetColor();
                return;
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
            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            try
            {
                AddonHandle.ExtractFile(filename, extractPath);
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

            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
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

            try
            {
                AddonHandle.ExportFile(filename, exportPath);
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
            AddonHandle.WatchedFiles.Where(f => f.ContentPath == filename).First().FileChanged +=
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
                    AddonHandle.WatchedFiles.Where(f => f.FilePath == e.FullPath).First().ContentPath + " changed!");
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

            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            try
            {
                AddonHandle.DropExport(filename);
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

            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
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

            try
            {
                AddonHandle.Pull(filename);
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

            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
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

            foreach (ContentFile f in AddonHandle.OpenAddon.Files)
            {
                Console.WriteLine("File index: " + f.Path.TrimStart('/') + " [CRC: " + f.CRC + "] [Size:" + ((int)f.Size).HumanReadableSize() + "]");
            }

            try
            {
                AddonHandle.Save();
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
            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            string temppath;
            try
            {
                temppath = Path.GetTempPath() + "/" + Path.GetFileName(path);

                try
                {
                    File.WriteAllBytes(temppath, AddonHandle.GetFile(path).Content);
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
            {
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Closes the currently open addon connection.
        /// </summary>
        private static void CloseAddon(bool forced = false)
        {
            if (AddonHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No addon is open.");
                Console.ResetColor();
                return;
            }

            if (AddonHandle != null && !forced && AddonHandle.Modified)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The open addon has been modified!");
                Console.ResetColor();
                Console.WriteLine("Please save it (push) or use forced close (fclose) to drop the changes.");
                return;
            }

            AddonHandle.Close();
            AddonHandle = null;

            // Closing an addon reenables the whitelist.
            Whitelist.Override = false;
        }
    }
}