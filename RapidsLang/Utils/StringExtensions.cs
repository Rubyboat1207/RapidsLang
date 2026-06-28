using System.Text.RegularExpressions;

namespace RapidsLang.Utils;

public static class StringExtensions
{
    
    public static string Unescape(this string s) => Regex.Replace(s, @"\\.", m => m.Value switch {
        @"\n" => "\n",
        @"\t" => "\t",
        @"\r" => "\r",
        @"\\" => "\\",
        @"\e" => "\e",
        @"\a" => "\a",
        @"\b" => "\b",
        @"\f" => "\f",
        _ => m.Value  // leave unknown sequences as-is
    });
}