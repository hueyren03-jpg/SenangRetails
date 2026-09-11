using QuestPDF.Drawing;

namespace SenangRetails.Shared.Services;

public static class PdfFontRegistration
{
    public const string FontFamily = "Noto Sans SC";

    private static readonly object RegistrationLock = new();
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered)
            return;

        lock (RegistrationLock)
        {
            if (_isRegistered)
                return;

            var assembly = typeof(PdfFontRegistration).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name =>
                    name.EndsWith("NotoSansSC-Regular.ttf", StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                throw new InvalidOperationException(
                    "The embedded PDF font NotoSansSC-Regular.ttf could not be found in SenangRetails.Shared.");
            }

            using var fontStream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"The embedded PDF font resource '{resourceName}' could not be opened.");

            FontManager.RegisterFontWithCustomName(FontFamily, fontStream);
            _isRegistered = true;
            Console.WriteLine($"[PDF] Registered embedded font '{FontFamily}' from '{resourceName}'.");
        }
    }
}
