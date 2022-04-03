﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SoulsIds;
using static SoulsIds.GameSpec;

namespace ESDLang.Script
{
    class OptionsConfig
    {
        // Simple JSON representation of ESDOptions.
        // This is required for Yabber-style drag-and-drop runs. It is a strict subset of command line functionality.
        [JsonProperty(PropertyName = "game")]
        public string Game { get; set; }
        [JsonProperty(PropertyName = "basedir")]
        public string BaseDir { get; set; }
        [JsonProperty(PropertyName = "backup")]
        public bool Backup { get; set; }
        [JsonProperty(PropertyName = "extra")]
        public Dictionary<string, string> ExtraESDs { get; set; }
        [JsonProperty(PropertyName = "other_options")]
        public string Options { get; set; }

        public static OptionsConfig GetOrCreate(string file)
        {
            string configDir = Path.GetDirectoryName(file);
            string configPath = Path.Combine(configDir, "esdtoolconfig.json");
            configPath = new FileInfo(configPath).FullName;
            if (File.Exists(configPath))
            {
                try
                {
                    OptionsConfig readConfig = JsonConvert.DeserializeObject<OptionsConfig>(File.ReadAllText(configPath));
                    Console.WriteLine($"Using config {configPath}");
                    Console.WriteLine();
                    return readConfig;
                }
                catch (JsonException ex)
                {
                    throw new Exception($"Failed to parse {configPath}. Please either fix it or delete it so it can be recreated.", ex);
                }
            }

            // Guided approach
            Console.WriteLine($"Creating {configPath}");
            Console.WriteLine();
            FromGame game = FromGame.UNKNOWN;
            while (game == FromGame.UNKNOWN)
            {
                Console.WriteLine($"Supported games: [{string.Join(", ", ESDOptions.Games.Names.Keys)}]");
                Console.Write("Select a game type: ");
                string text = Console.ReadLine();
                if (text == null) return null;
                text = text.Trim().ToLowerInvariant();
                if (!ESDOptions.Games.Names.TryGetValue(text, out game))
                {
                    Console.WriteLine();
                    Console.WriteLine($"Error: Unrecognized game type \"{text}\"");
                }
                Console.WriteLine();
            }
            GameSpec gameInfo = ForGame(game);

            string baseDir = gameInfo.GameDir;
            if (Directory.Exists(baseDir))
            {
                Console.WriteLine($"Detected game directory: {baseDir}");
                Console.WriteLine("Make sure to unpack your game with UXM/UDSFM first.");
                Console.Write($"Use this directory [y/n]? ");
                string text = Console.ReadLine();
                if (text == null) return null;
                if (!text.Trim().ToLowerInvariant().StartsWith("y"))
                {
                    baseDir = null;
                }
                Console.WriteLine();
            }
            else
            {
                baseDir = null;
            }
            while (baseDir == null)
            {
                Console.WriteLine("Unpack your game with UXM/UDSFM and paste the game directory here. (Right-click to paste.)");
                Console.Write($"Enter {game} game directory: ");
                string text = Console.ReadLine();
                if (text == null) return null;
                text = text.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    try
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(text);
                        if (!Path.IsPathRooted(text))
                        {
                            Console.WriteLine();
                            Console.WriteLine($"Error: Provide an absolute directory, not a relative one");
                        }
                        else if (!dirInfo.Exists)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"Error: Directory \"{dirInfo.FullName}\" not found");
                        }
                        else
                        {
                            baseDir = dirInfo.FullName;
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
                Console.WriteLine();
            }

            bool backup = false;
            {
                Console.Write($"Create backups of overwritten files [y/n]? ");
                string text = Console.ReadLine();
                if (text == null) return null;
                if (text.Trim().ToLowerInvariant().StartsWith("y"))
                {
                    backup = true;
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Writing config {configPath}");
            Console.WriteLine();
            Console.WriteLine("Use the command line interface directly if you want to access exciting advanced functionality. You can also edit the config.");
            Console.WriteLine();
            OptionsConfig config = new OptionsConfig
            {
                Game = game.ToString().ToLowerInvariant(),
                BaseDir = baseDir,
                Backup = backup,
                Options = "",
                ExtraESDs = new Dictionary<string, string>(),
            };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented) + Environment.NewLine);
            return config;
        }

        public List<string> MakeOptions(IList<string> files)
        {
            List<string> ret = new List<string>();
            // Brain-dead options list, no validation
            // Aim for precision rather than massaging bad inputs
            if (!string.IsNullOrWhiteSpace(Game))
            {
                ret.Add($"-{Game}");
            }
            if (!string.IsNullOrWhiteSpace(BaseDir))
            {
                ret.Add($"-basedir");
                ret.Add(BaseDir);
            }
            if (Backup)
            {
                ret.Add($"-backup");
            }
            if (ExtraESDs != null && ExtraESDs.Count > 0)
            {
                ret.Add($"-extra");
                foreach (KeyValuePair<string, string> entry in ExtraESDs)
                {
                    ret.Add($"{entry.Key}={entry.Value}");
                }
            }
            if (!string.IsNullOrWhiteSpace(Options))
            {
                ret.AddRange(SplitCommandLine(Options));
            }
            List<string> errors = new List<string>();
            // Check in advance if there are multiple bnd files to unpack, so we can do it in directory, which is the preferred workflow
            Dictionary<string, List<string>> bndsByDirectory = new Dictionary<string, List<string>>();
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    string name = fileInfo.Name;
                    string dir = fileInfo.DirectoryName;
                    if (name.EndsWith(".talkesdbnd") || name.EndsWith(".talkesdbnd.dcx"))
                    {
                        if (!bndsByDirectory.TryGetValue(dir, out List<string> names))
                        {
                            bndsByDirectory[dir] = names = new List<string>();
                        }
                        names.Add(fileInfo.FullName);
                    }
                }
            }
            Dictionary<string, string> directoryBndTargets = new Dictionary<string, string>();
            foreach (KeyValuePair<string, List<string>> entry in bndsByDirectory)
            {
                foreach (string bnd in entry.Value)
                {
                    Console.WriteLine(bnd);
                }
                Console.WriteLine();
                if (entry.Value.Count == 1)
                {
                    Console.WriteLine($"Note: ESD files from multiple different bnd files can all be decompiled to the same directory. When the directory is recompiled, it automatically updates all of the bnds containing those files.");
                    Console.WriteLine();
                    Console.WriteLine("Enter a directory name to enable editing multiple bnds, or else enter nothing to create a directory limited to only this bnd file.");
                }
                else
                {
                    Console.WriteLine($"You've selected multiple bnd files in the same directory. Decompiled files from different bnds can be added to a single directory. When the directory is recompiled, it updates all of the bnds containing those files.");
                    Console.WriteLine();
                    Console.WriteLine("Enter a directory name for all decompiled files, or else enter nothing to create separate directories limited to only their bnd files.");
                }
                Console.WriteLine();
                while (true)
                {
                    Console.Write("Single directory to write to: ");
                    string text = Console.ReadLine();
                    if (text == null) return null;
                    text = text.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        break;
                    }
                    if (text.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                    {
                        directoryBndTargets[entry.Key] = text;
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid directory name");
                        Console.WriteLine();
                    }
                }
            }
            Console.WriteLine();
            Dictionary<(string, string), List<string>> inputsByOutput = new Dictionary<(string, string), List<string>>();
            void addInput(string operation, string input, string output)
            {
                (string, string) key = (operation, output);
                if (!inputsByOutput.TryGetValue(key, out List<string> inputs))
                {
                    inputsByOutput[key] = inputs = new List<string>();
                }
                inputsByOutput[key].Add(input);
            }
            foreach (string file in files)
            {
                // Don't validate these file paths too hard, as they should come from drag-and-drop
                if (Directory.Exists(file))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(file);
                    List<string> pys = Directory.GetFiles(dirInfo.FullName, "*.py").ToList();
                    string parent = dirInfo.Parent.FullName;
                    if (dirInfo.Name.EndsWith("-only"))
                    {
                        string prefix = dirInfo.Name.Substring(0, dirInfo.Name.LastIndexOf("-only"));
                        string pattern = $"{prefix}.*esdbnd";
                        List<string> match = Directory.GetFiles(parent, pattern).Concat(Directory.GetFiles(parent, pattern + ".dcx")).ToList();
                        if (match.Count == 0)
                        {
                            errors.Add($"Can't pack {dirInfo.FullName}: No ESD files for {prefix} found in {dirInfo.Parent.Name} directory");
                        }
                        else if (match.Count > 1)
                        {
                            errors.Add($"Can't pack {dirInfo.FullName}: Multiple ESD files matching {prefix} found in {dirInfo.Parent.Name} directory");
                        }
                        else
                        {
                            foreach (string py in pys)
                            {
                                addInput("writebndfile", py, match[0]);
                            }
                        }
                    }
                    else
                    {
                        if (Directory.GetFiles(parent, "*esdbnd.dcx").Count() == 0 && Directory.GetFiles(parent, "*esdbnd").Count() == 0)
                        {
                            // This is fine actually
                            // errors.Add($"Can't pack {dirInfo.FullName}: no ESD BND files found in {dirInfo.Parent.Name} directory");
                        }
                        foreach (string py in pys)
                        {
                            addInput("writebnd", py, parent);
                        }
                    }
                }
                else if (File.Exists(file))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    string name = fileInfo.Name;
                    if (name.EndsWith(".py"))
                    {
                        addInput("writeloose", fileInfo.FullName, Path.Combine(fileInfo.DirectoryName, "%e.esd"));
                    }
                    else if (name.EndsWith(".esd") || name.EndsWith(".esd.dcx"))
                    {
                        addInput("writepy", fileInfo.FullName, Path.Combine(fileInfo.DirectoryName, "%e.py"));
                    }
                    else if (name.EndsWith(".talkesdbnd") || name.EndsWith(".talkesdbnd.dcx"))
                    {
                        if (!directoryBndTargets.TryGetValue(fileInfo.DirectoryName, out string relDirName))
                        {
                            relDirName = name.Substring(0, name.LastIndexOf(".talkesdbnd")) + "-only";
                        }
                        addInput("writepy", fileInfo.FullName, Path.Combine(Path.Combine(fileInfo.DirectoryName, relDirName), "%e.py"));
                    }
                    else
                    {
                        errors.Add($"{file} is not named like an Python file, ESD, or talk ESD BND");
                    }
                }
                else
                {
                    errors.Add($"{file} not found");
                }
            }
            foreach (KeyValuePair<(string, string), List<string>> entry in inputsByOutput)
            {
                (string operation, string output) = entry.Key;
                ret.Add("-i");
                ret.AddRange(entry.Value);
                ret.Add("-" + operation);
                ret.Add(output);
            }
            if (errors.Count > 0)
            {
                Console.WriteLine("Error: Unrecognized files:");
                foreach (string error in errors) Console.WriteLine(error);
                Console.WriteLine();
                return null;
            }
            return ret;
        }

        private static List<string> SplitCommandLine(string str)
        {
            // This sure would be great functionality to have in a language's standard library
            List<string> args = new List<string>();
            StringBuilder sb = new StringBuilder();
            char? quoteCh = null;
            foreach (char ch in str)
            {
                // Basic paired quote marks. Don't try to handle escaping
                if (ch == '"' || ch == '\'')
                {
                    if (quoteCh is char prevCh && ch == prevCh)
                    {
                        quoteCh = null;
                    }
                    else if (quoteCh is null)
                    {
                        quoteCh = ch;
                    }
                }
                if (quoteCh is null && ch == ' ')
                {
                    if (sb.Length > 0)
                    {
                        args.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            if (sb.Length > 0)
            {
                args.Add(sb.ToString());
            }
            string unquote(string text)
            {
                if (text.Length >= 2 && (text[0] == '"' || text[0] == '\'') && text[0] == text[text.Length - 1])
                {
                    return text.Substring(1, text.Length - 2);
                }
                return text;
            }
            return args.Select(unquote).ToList();
        }
    }
}
