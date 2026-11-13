using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    // Main application entry point
    static int Main(string[] args)
    {
        Console.BackgroundColor = ConsoleColor.White;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Clear();
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length < 1 || args.Length > 2)
        {
            Console.WriteLine("JavaScript to FreeBASIC Converter");
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Usage:");
            Console.WriteLine("  converter.exe input.js [output.cs]");
            Console.WriteLine();
            Console.WriteLine("If output.cs is omitted, the output file uses the input name with .cs extension.");
            return 1;
        }

        string inputFile = args[0];

        if (!File.Exists(inputFile))
        {
            Console.WriteLine("Error: input file not found: " + inputFile);
            return 1;
        }

        string outputFile = args.Length == 2
            ? args[1]
            : Path.ChangeExtension(inputFile, ".cs");

        try
        {
            string jsCode = File.ReadAllText(inputFile, Encoding.UTF8);
            string convertedCode = ConvertJsToFreeBasic(jsCode);

            // Display converted code in the console
            Console.WriteLine("Generated FreeBASIC Preview:");
            Console.WriteLine("--------------------------------");
            Console.WriteLine(convertedCode);
            Console.WriteLine("--------------------------------");

            // Save converted code to the output file
            File.WriteAllText(outputFile, convertedCode, new UTF8Encoding(false));

            Console.WriteLine("Converted \"" + Path.GetFileName(inputFile) + "\" successfully!");
            Console.WriteLine("Output file: " + outputFile);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    // Main transpilation engine: converts JavaScript patterns to FreeBASIC
    static string ConvertJsToFreeBasic(string jsSource)
    {
        string[] lines = Regex.Split(jsSource, @"\r?\n");
        List<string> result = new List<string>();
        Stack<string> blockStack = new Stack<string>();

        result.Add("' ==============================================");
        result.Add("' Transpiled from JavaScript to FreeBASIC");
        result.Add("' ==============================================\n");

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // Skip blank lines
            if (line == "")
            {
                result.Add("");
                continue;
            }

            // Convert single line comments // to '
            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                result.Add(Regex.Replace(line, @"^//", "'"));
                continue;
            }

            // Convert multi-line comment delimiters
            line = line.Replace("/*", "/'").Replace("*/", "'/");

            // Handle closing braces
            if (line == "}" || line.StartsWith("}", StringComparison.Ordinal))
            {
                string top = blockStack.Count > 0 ? blockStack.Pop() : "";

                if (top == "function")
                    result.Add("End Function");
                else if (top == "sub")
                    result.Add("End Sub");
                else if (top == "if")
                    result.Add("End If");
                else if (top == "for")
                    result.Add("Next");
                else if (top == "while")
                    result.Add("Wend");
                else
                    result.Add("' End Block");

                // If the line was only "}", move on
                if (line == "}")
                    continue;

                // Otherwise, process the rest of the line (e.g. "} else {")
                line = line.Substring(1).Trim();
            }

            // Map print(...), message.log(...) and console.log(...) to Print
            line = Regex.Replace(line, @"\bmessage\.log\s*\(", "Print ");
            line = Regex.Replace(line, @"\bconsole\.log\s*\(", "Print ");
            line = Regex.Replace(line, @"\bprint\s*\(", "Print ");

            // If line begins with Print, clean trailing parentheses and semicolons
            if (Regex.IsMatch(line, @"^Print\b"))
            {
                line = Regex.Replace(line, @"\);\s*$", "");
                line = Regex.Replace(line, @"\)\s*$", "");
            }

            // Map function declarations
            Match funcMatch = Regex.Match(
                line,
                @"^function\s+([a-zA-Z0-9_$]+)\s*\((.*?)\)\s*\{?"
            );

            if (funcMatch.Success)
            {
                string funcName = funcMatch.Groups[1].Value;
                string parameterText = funcMatch.Groups[2].Value;

                List<string> parameters = new List<string>();
                foreach (string parameter in parameterText.Split(','))
                {
                    string p = parameter.Trim();
                    if (p.Length > 0)
                        parameters.Add(p);
                }

                List<string> fbParameters = new List<string>();
                foreach (string parameter in parameters)
                    fbParameters.Add(parameter + " As Any");

                blockStack.Push("function");
                result.Add("Function " + funcName + "(" +
                           string.Join(", ", fbParameters) + ") As Any");
                continue;
            }

            // Map variable declarations
            line = Regex.Replace(
                line,
                @"\b(let|var|const)\s+([a-zA-Z0-9_$]+)\s*=\s*",
                "Var $2 = "
            );

            line = Regex.Replace(
                line,
                @"\b(let|var|const)\s+([a-zA-Z0-9_$]+);?",
                "Dim As Any $2"
            );

            // Map if statements
            if (Regex.IsMatch(line, @"^if\s*\("))
            {
                line = Regex.Replace(
                    line,
                    @"^if\s*\((.*)\)\s*\{?$",
                    "If $1 Then"
                );
                blockStack.Push("if");
            }
            else if (Regex.IsMatch(line, @"^else\s+if\s*\("))
            {
                line = Regex.Replace(
                    line,
                    @"^else\s+if\s*\((.*)\)\s*\{?$",
                    "ElseIf $1 Then"
                );
            }
            else if (Regex.IsMatch(line, @"^else\s*\{?$"))
            {
                line = "Else";
            }

            // Map while loops
            if (Regex.IsMatch(line, @"^while\s*\("))
            {
                line = Regex.Replace(
                    line,
                    @"^while\s*\((.*)\)\s*\{?$",
                    "While $1"
                );
                blockStack.Push("while");
            }

            // Map basic for loops
            Match forMatch = Regex.Match(
                line,
                @"^for\s*\(\s*(?:var|let)?\s*([a-zA-Z0-9_$]+)\s*=\s*([^;]+);\s*\1\s*<\s*([^;]+);\s*(?:\1\+\+|\+\+\1|\1\s*\+=\s*1)\s*\)\s*\{?"
            );

            if (forMatch.Success)
            {
                string varName = forMatch.Groups[1].Value;
                string startValue = forMatch.Groups[2].Value.Trim();
                string endValue = forMatch.Groups[3].Value.Trim();

                blockStack.Push("for");
                result.Add("For " + varName + " = " + startValue +
                           " To (" + endValue + ") - 1");
                continue;
            }

            // Convert operators and constants
            line = line.Replace("===", "=")
                       .Replace("==", "=")
                       .Replace("!==", "<>")
                       .Replace("!=", "<>")
                       .Replace("&&", " And ")
                       .Replace("||", " Or ");

            line = Regex.Replace(line, @"!([a-zA-Z0-9_$(])", "Not $1");
            line = Regex.Replace(line, @"\btrue\b", "True");
            line = Regex.Replace(line, @"\bfalse\b", "False");
            line = Regex.Replace(line, @"\bnull\b", "NULL");

            // Clean standalone opening braces and trailing semicolons
            line = Regex.Replace(line, @"\s*\{$", "");
            line = Regex.Replace(line, @";\s*$", "");

            result.Add(line);
        }

        // Close any unclosed blocks
        while (blockStack.Count > 0)
        {
            string remaining = blockStack.Pop();

            if (remaining == "function")
                result.Add("End Function");
            else if (remaining == "if")
                result.Add("End If");
            else if (remaining == "for")
                result.Add("Next");
            else if (remaining == "while")
                result.Add("Wend");
        }

        return string.Join("\n", result);
    }
}
