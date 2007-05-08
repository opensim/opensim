using System;
using System.Collections.Generic;
using System.Text;

namespace libsecondlife.TestClient {
    class Parsing {
        public static string[] ParseArguments(string str) {
            List<string> list = new List<string>();
            string current = "";
            string trimmed = null;
            bool withinQuote = false;
            bool escaped = false;
            foreach (char c in str) {
                if (c == '"') {
                    if (escaped) {
                        current += '"';
                        escaped = false;
                    } else {
                        current += '"';
                        withinQuote = !withinQuote;
                    }
                } else if (c == ' ' || c == '\t') {
                    if (escaped || withinQuote) {
                        current += c;
                        escaped = false;
                    } else {
                        trimmed = current.Trim();
                        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) {
                            trimmed = trimmed.Remove(0, 1);
                            trimmed = trimmed.Remove(trimmed.Length - 1);
                            trimmed = trimmed.Trim();
                        }
                        if (trimmed.Length > 0)
                            list.Add(trimmed);
                        current = "";
                    }
                } else if (c == '\\') {
                    if (escaped) {
                        current += '\\';
                        escaped = false;
                    } else {
                        escaped = true;
                    }
                } else {
                    if (escaped)
                        throw new FormatException(c.ToString() + " is not an escapable character.");
                    current += c;
                }
            }
            trimmed = current.Trim();
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) {
                trimmed = trimmed.Remove(0, 1);
                trimmed = trimmed.Remove(trimmed.Length - 1);
                trimmed = trimmed.Trim();
            }
            if (trimmed.Length > 0)
                list.Add(trimmed);
            return list.ToArray();
        }
    }
}
