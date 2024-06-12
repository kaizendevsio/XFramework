using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

Console.WriteLine("Please choose an option:");
Console.WriteLine("1. Add MemoryPack attributes to classes");

var option = Console.ReadLine();

switch (option)
{
    case "1":
        Console.WriteLine("Enter the directory path:");
        var directoryPath = Console.ReadLine();

        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine("Directory does not exist.");
            return;
        }
        AddMemoryPackAttributes(directoryPath);
        break;
    default:
        Console.WriteLine("Invalid option selected.");
        break;
}

static void AddMemoryPackAttributes(string directoryPath)
{
    var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.TopDirectoryOnly);

    foreach (var file in csFiles)
    {
        Console.WriteLine($"Processing file: {file}");
        var lines = File.ReadAllLines(file);
        var classFound = false;
        var propertyOrder = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                continue;
            }
            
            if (lines[i].Contains("public partial class"))
            {
                // Check if the MemoryPackable attribute is already present within a few lines above the class declaration
                if (i > 0 && !Regex.IsMatch(lines[i - 1], @"\[MemoryPackable\(GenerateType\.CircularReference\)\]"))
                {
                    lines[i] = "[MemoryPackable(GenerateType.CircularReference)]\n" + lines[i];
                }
                classFound = true;
                propertyOrder = 0;
                continue;
            }

            if (classFound && lines[i].Trim().StartsWith("public") && !lines[i].Contains("partial") && !lines[i].Contains("class"))
            {
                var indent = new string(' ', lines[i].TakeWhile(char.IsWhiteSpace).Count());
                var propertyLine = lines[i].Trim();

                // Handle properties with custom getters or setters
                if (propertyLine.Contains("=>"))  // Remove existing MemoryPackOrder if present
                {
                    if (lines[i - 1].Trim().StartsWith("[MemoryPackIgnore"))
                    {
                        continue;
                    }
                    lines[i] = $"{indent}[MemoryPackIgnore]\n{lines[i]}";
                    continue;
                }

                if (lines[i - 1].Trim().StartsWith("[MemoryPackOrder") && lines[i - 1].Trim().EndsWith("]"))  // Remove existing MemoryPackOrder if present
                {
                    lines[i - 1] = $"[MemoryPackOrder({propertyOrder})]";
                }
                else
                {
                    lines[i] = $"{indent}[MemoryPackOrder({propertyOrder})]\n{lines[i]}";
                }

                propertyOrder++;
            }
            
            
            if (classFound && lines[i].Trim().Equals("}"))
            {
                classFound = false; // End of class
            }
        }

        File.WriteAllLines(file, lines);
        Console.WriteLine($"Modified and saved: {file}");
    }

    Console.WriteLine("Processing complete.");
}
