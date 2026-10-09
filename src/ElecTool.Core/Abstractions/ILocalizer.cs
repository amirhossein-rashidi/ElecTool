namespace ElecTool.Core.Abstractions
{
    public interface ILocalizer
    {
        /// <summary>Current language code: "fa" or "en".</summary>
        string Language { get; set; }

        /// <summary>Returns the text for a key. Unknown keys return the key itself.</summary>
        string Get(string key);

        /// <summary>Returns the text for a key, formatted with the given arguments.</summary>
        string Get(string key, params object[] args);
    }
}
