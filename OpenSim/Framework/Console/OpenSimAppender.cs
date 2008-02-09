using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

using log4net.Core;
using log4net.Layout;
using log4net.Appender;
using log4net.Util;

namespace OpenSim.Framework.Console
{
    public class OpenSimAppender : AnsiColorTerminalAppender
    {
        override protected void Append(LoggingEvent le)
        {
            string loggingMessage = RenderLoggingEvent(le);
            string regex = @"^(?<Front>.*?)\[(?<Category>[^\]]+)\]:?(?<End>.*)";

            Regex RE = new Regex(regex, RegexOptions.Multiline);
            MatchCollection matches = RE.Matches(loggingMessage);
            // Get some direct matches $1 $4 is a 
            if (matches.Count == 1)
            {
                System.Console.Write(matches[0].Groups["Front"].Value);
                System.Console.Write("[");

                WriteColorText(DeriveColor(matches[0].Groups["Category"].Value), matches[0].Groups["Category"].Value);
                System.Console.Write("]:");
                
                if (le.Level == Level.Error) 
                {
                    WriteColorText(ConsoleColor.Red, matches[0].Groups["End"].Value);
                }
                else if (le.Level == Level.Warn) 
                {
                    WriteColorText(ConsoleColor.Yellow, matches[0].Groups["End"].Value);
                }
                else 
                {
                    System.Console.Write(matches[0].Groups["End"].Value);
                }
                System.Console.WriteLine();
            } 
            else
            {
                System.Console.Write(loggingMessage);
            }
        }

        private void WriteColorText(ConsoleColor color, string sender)
        {
            try
            {
                lock (this)
                {
                    try
                    {
                        System.Console.ForegroundColor = color;
                        System.Console.Write(sender);
                        System.Console.ResetColor();
                    }
                    catch (ArgumentNullException)
                    {
                        // Some older systems dont support coloured text.
                        System.Console.WriteLine(sender);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private ConsoleColor DeriveColor(string input)
        {
            int colIdx = (input.ToUpper().GetHashCode() % 6) + 9;
            return (ConsoleColor) colIdx;
        }

    }
}
