using System;
using System.Globalization;
using System.Resources;

namespace DMXVideoPlayer
{
    /// <summary>
    /// Singleton service providing FR/EN localization backed by .resx resources.
    /// The selected language is applied at startup and persisted; changing it requires
    /// an application restart to take effect (kept intentionally simple, no live UI refresh).
    /// </summary>
    public sealed class LocalizationService
    {
        public static LocalizationService Instance { get; } = new LocalizationService();

        private static readonly ResourceManager ResourceManager =
            new ResourceManager("DMXVideoPlayer.Resources.Strings", typeof(LocalizationService).Assembly);

        private CultureInfo _currentCulture;

        private LocalizationService()
        {
            _currentCulture = DetectDefaultCulture();
        }

        public CultureInfo CurrentCulture => _currentCulture;

        /// <summary>Indexer used by AXAML bindings: {Binding [Key], Source={x:Static local:LocalizationService.Instance}}</summary>
        public string this[string key] => ResourceManager.GetString(key, _currentCulture) ?? key;

        /// <summary>Sets the current language. Accepts two-letter ISO codes such as "fr" or "en". Requires an application restart to be reflected in the UI.</summary>
        public void SetLanguage(string twoLetterCode)
        {
            _currentCulture = NormalizeCulture(twoLetterCode);
        }

        public static CultureInfo DetectDefaultCulture()
        {
            var systemCulture = CultureInfo.CurrentUICulture;
            return NormalizeCulture(systemCulture.TwoLetterISOLanguageName);
        }

        private static CultureInfo NormalizeCulture(string? twoLetterCode)
        {
            return string.Equals(twoLetterCode, "en", StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("en")
                : CultureInfo.GetCultureInfo("fr");
        }
    }
}
