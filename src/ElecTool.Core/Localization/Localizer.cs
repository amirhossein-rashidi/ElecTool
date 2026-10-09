using System;
using System.Globalization;
using ElecTool.Core.Abstractions;

namespace ElecTool.Core.Localization
{
    public sealed class Localizer : ILocalizer
    {
        public const string Persian = "fa";
        public const string English = "en";

        private string _language;

        public Localizer(string language)
        {
            _language = IsSupported(language) ? language : Persian;
        }

        public static bool IsSupported(string? language)
        {
            return language == Persian || language == English;
        }

        public string Language
        {
            get { return _language; }
            set
            {
                if (!IsSupported(value))
                {
                    throw new ArgumentException("Unsupported language: " + value, nameof(value));
                }

                _language = value;
            }
        }

        public string Get(string key)
        {
            return Lookup(key);
        }

        public string Get(string key, params object[] args)
        {
            string text = Lookup(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        private string Lookup(string key)
        {
            if (Strings.Table.TryGetValue(key, out var entry))
            {
                string text = _language == Persian ? entry.Fa : entry.En;
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }

                if (!string.IsNullOrEmpty(entry.En))
                {
                    return entry.En;
                }
            }

            return key;
        }
    }
}
