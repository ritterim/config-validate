using CommandLine;
using ConsoleTables;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConfigValidate
{
    class Program
    {
        static int Main(string[] args)
        {
            var dotFilePath = Path.Combine(Environment.CurrentDirectory, ".config-validate");
            Options options = null;
            ParserResult<Options> parserResult = null;

            if (File.Exists(dotFilePath))
            {
                Console.WriteLine($"Found dotfile at `{dotFilePath}`. Attempting to read.");

                try
                {
                    options = JsonConvert.DeserializeObject<Options>(File.ReadAllText(dotFilePath));

                    Console.WriteLine("Successfully read dotfile.");
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("Could not read or use dotfile. Using default settings.");

                    options = new Options();
                }
            }
            else
            {
                Console.WriteLine($"Attempting to read arguments.");

                parserResult = Parser.Default.ParseArguments<Options>(args);
            }

            if (parserResult is NotParsed<Options>)
            {
                return -1;
            }
            else
            {
                if (options == null)
                {
                    Console.WriteLine("Applying arguments.");

                    options = (parserResult as Parsed<Options>).Value;
                }

                Console.WriteLine();

                var ignorePaths = options.IgnorePaths.ToList();
                var failureStates = new List<FailureState>();
                var showStates = options.Show.ToList();
                var files = Directory.GetFiles(Environment.CurrentDirectory, "appsettings*.json");
                var blueprintFile = files
                    .SingleOrDefault(x => Regex.IsMatch(x, "appsettings\\.json", RegexOptions.IgnoreCase));

                if (string.IsNullOrEmpty(blueprintFile))
                {
                    Console.Error.WriteLine("Could not find default `appsettings.json` file. Please run this tool in the directory where this file resides.");

                    return -1;
                }

                var environmentFiles = files.Except(new[] { blueprintFile });
                var blueprintConfiguration = new ConfigurationBuilder().AddJsonFile(blueprintFile).Build();
                var blueprintFlattened = FlattenConfiguration(blueprintConfiguration.GetChildren());

                var environmentsFlattened = environmentFiles
                    .Select(environmentFilePath =>
                    {
                        var environmentName = Regex
                            .Match(environmentFilePath, "appsettings\\.(?<environment>.*?)\\.json$", RegexOptions.IgnoreCase)
                            .Groups["environment"]
                            ?.Value ?? environmentFilePath;

                        var environmentConfiguration = new ConfigurationBuilder().AddJsonFile(environmentFilePath).Build();

                        return new
                        {
                            flattenedConfiguration = FlattenConfiguration(environmentConfiguration.GetChildren()),
                            name = environmentName,
                            path = environmentFilePath
                        };
                    })
                    .Where(x => options.Environments.Contains(x.name, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (environmentsFlattened.Count != options.Environments.Count())
                {
                    Console.Error.WriteLine(
                        $"Could not find all appsettings-files based on `{nameof(Options.Environments)}` argument: {string.Join(',', options.Environments)}.");

                    return -1;
                }

                foreach (var environmentFlattened in environmentsFlattened)
                {
                    Console.WriteLine(environmentFlattened.name.ToUpper());

                    if (environmentFlattened.name != environmentFlattened.path)
                    {
                        Console.WriteLine(environmentFlattened.path);
                    }

                    var table = new ConsoleTable("Key", "Unknown", "Missing", "Value", "Ignored");

                    table.Options.EnableCount = false;

                    var combinedFlattenedConfiguration = blueprintFlattened.ToList().Union(environmentFlattened.flattenedConfiguration.ToList()).ToList();

                    foreach (var kvp in combinedFlattenedConfiguration)
                    {
                        if (kvp is ParentAppSetting)
                        {
                            table.AddRow(kvp.DisplayKey, null, null, null, null);
                        }
                        else
                        {
                            var ignoreKey = ignorePaths.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase);
                            var isUnknown = !(blueprintFlattened.FirstOrDefault(x => x.Key == kvp.Key) != null);
                            var isMissing = !(environmentFlattened.flattenedConfiguration.FirstOrDefault(x => x.Key == kvp.Key) != null);
                            var currentFailureStates = new List<FailureState>();

                            if (!ignoreKey)
                            {
                                if (isUnknown)
                                {
                                    currentFailureStates.Add(FailureState.Unknown);

                                    if (!failureStates.Contains(FailureState.Unknown))
                                    {
                                        failureStates.Add(FailureState.Unknown);
                                    }
                                }

                                if (isMissing)
                                {
                                    currentFailureStates.Add(FailureState.Missing);

                                    if (!failureStates.Contains(FailureState.Missing))
                                    {
                                        failureStates.Add(FailureState.Missing);
                                    }
                                }
                            }

                            if (
                                !showStates.Any() ||
                                showStates.Any(x => currentFailureStates.Contains(x)))
                            table.AddRow(
                                kvp.DisplayKey,
                                isUnknown
                                    ? "Yes"
                                    : "No",
                                isMissing
                                    ? "Yes"
                                    : "No",
                                kvp.Value.Length > 27
                                    ? kvp.Value.Substring(0, 27) + "..."
                                    : kvp.Value,
                                ignoreKey
                                    ? "Yes"
                                    : "No");
                        }
                    }

                    table.Write();
                }

                if (failureStates.Any(x => options.FailureStates.Contains(x)))
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private static List<AppSetting> FlattenConfiguration(
            IEnumerable<IConfigurationSection> configurationSections,
            string parentKey = null)
        {
            var flattenedConfiguration = new List<AppSetting>();

            foreach (var section in configurationSections)
            {
                var key = string.IsNullOrEmpty(parentKey) ? $"{section.Key}" : $"{parentKey}:{section.Key}";
                var indentSpacing = string.Join(
                    string.Empty,
                    Enumerable.Repeat("--", parentKey?.Split(':').Count() ?? 0));
                var appSetting = new AppSetting()
                {
                    DisplayKey = string.IsNullOrEmpty(parentKey) ? $"{section.Key}" : $"{indentSpacing}> {section.Key}",
                    Key = key,
                    Value = section.Value
                };

                var grandchildren = section.GetChildren().ToList();

                if (grandchildren.Any())
                {
                    flattenedConfiguration.Add(new ParentAppSetting(appSetting));

                    foreach (var kvp in FlattenConfiguration(section.GetChildren(), key))
                    {
                        flattenedConfiguration.Add(kvp);
                    }
                }
                else
                {
                    flattenedConfiguration.Add(appSetting);
                }
            }

            return flattenedConfiguration;
        }

        private class AppSetting
        {
            public string DisplayKey { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
        }

        private class ParentAppSetting : AppSetting
        {
            public ParentAppSetting(AppSetting appSetting)
            {
                DisplayKey = appSetting.DisplayKey;
                Key = appSetting.Key;
                Value = appSetting.Value;
            }
        }

        private enum FailureState
        {
            Missing,
            Unknown
        }

        private class Options
        {
            [Option('e', "environments", Separator = ',', Required = false, Default = new[] { "Production" })]
            public IEnumerable<string> Environments { get; set; } = new[] { "Production" };

            [Option('f', "failure-states", Separator = ',', Required = false, Default = new[] { FailureState.Missing })]
            public IEnumerable<FailureState> FailureStates { get; set; } = new[] { FailureState.Missing };

            [Option('i', "ignore-paths", Separator = ',', Required = false)]
            public IEnumerable<string> IgnorePaths { get; set; } = Enumerable.Empty<string>();

            [Option('s', "show", Separator = ',', HelpText = "Only show app. settings matching a FailureState", Required = false)]
            public IEnumerable<FailureState> Show { get; set; } = Enumerable.Empty<FailureState>();
        }
    }
}
