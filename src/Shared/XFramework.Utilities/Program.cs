using System.Text.RegularExpressions;

var directoryPath = Console.ReadLine();
var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.TopDirectoryOnly);

foreach (var file in csFiles)
{
    Console.WriteLine($"Processing file: {file}");
    var lines = File.ReadAllLines(file);
    var classFound = false;
    var propertyOrder = 0; // Initialize the property order counter

    for (var i = 0; i < lines.Length; i++)
    {
        if (lines[i].Contains("public partial class") && (i == 0 || !lines[i - 1].Trim().StartsWith("[MemoryPackable")))
        {
            // Insert MemoryPackable attribute above the class declaration
            lines[i] = "[MemoryPackable(GenerateType.CircularReference)]\n" + lines[i];
            classFound = true; // Mark that we're inside a class
            propertyOrder = 0; // Reset the property order for each new class
        }

        if (classFound && lines[i].Contains("{ get; set; }") && lines[i].Trim().StartsWith("public"))
        {
            // Add MemoryPackOrder attribute to properties
            var indent = new string(' ', lines[i].TakeWhile(char.IsWhiteSpace).Count()); // Preserve indentation
            var propertyName = Regex.Match(lines[i], @"\bpublic \w+ (\w+)").Groups[1].Value;
            lines[i] = $"{indent}[MemoryPackOrder({propertyOrder})]\n{lines[i]}";
            propertyOrder++; // Increment the order for the next property
            
            continue;
        }

        if (classFound && lines[i].Contains("}"))
        {
            classFound = false; // Reset the flag when we leave a class
        }
    }

    // Write the modified content back to the file
    File.WriteAllLines(file, lines);
    Console.WriteLine($"Modified and saved: {file}");
}

Console.WriteLine("Processing complete.");