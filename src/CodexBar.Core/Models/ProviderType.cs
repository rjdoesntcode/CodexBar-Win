namespace CodexBar.Core.Models;

/// <summary>
/// Supported AI provider types
/// </summary>
public enum ProviderType
{
    Claude,
    Codex,
    Cursor,
    Copilot,
    Gemini,
    Augment,
    Amp,
    JetBrains,
    Kiro,
    Kimi,
    KimiK2,
    VertexAI,
    Zai,
    Factory,
    Antigravity,
    OpenCode,
    MiniMax
}

/// <summary>
/// Extension methods for ProviderType
/// </summary>
public static class ProviderTypeExtensions
{
    /// <summary>
    /// Gets the display name for the provider
    /// </summary>
    public static string GetDisplayName(this ProviderType provider) => provider switch
    {
        ProviderType.Claude => "Claude",
        ProviderType.Codex => "Codex",
        ProviderType.Cursor => "Cursor",
        ProviderType.Copilot => "GitHub Copilot",
        ProviderType.Gemini => "Gemini",
        ProviderType.Augment => "Augment",
        ProviderType.Amp => "Amp",
        ProviderType.JetBrains => "JetBrains AI",
        ProviderType.Kiro => "Kiro",
        ProviderType.Kimi => "Kimi",
        ProviderType.KimiK2 => "Kimi K2",
        ProviderType.VertexAI => "Vertex AI",
        ProviderType.Zai => "z.ai",
        ProviderType.Factory => "Factory (Droid)",
        ProviderType.Antigravity => "Antigravity",
        ProviderType.OpenCode => "OpenCode",
        ProviderType.MiniMax => "MiniMax",
        _ => provider.ToString()
    };

    /// <summary>
    /// Gets the website URL for the provider
    /// </summary>
    public static string GetWebsiteUrl(this ProviderType provider) => provider switch
    {
        ProviderType.Claude => "https://claude.ai",
        ProviderType.Codex => "https://openai.com",
        ProviderType.Cursor => "https://cursor.com",
        ProviderType.Copilot => "https://github.com/features/copilot",
        ProviderType.Gemini => "https://gemini.google.com",
        ProviderType.Augment => "https://augmentcode.com",
        ProviderType.Amp => "https://amp.dev",
        ProviderType.JetBrains => "https://www.jetbrains.com/ai",
        ProviderType.Kiro => "https://kiro.dev",
        ProviderType.Kimi => "https://kimi.moonshot.cn",
        ProviderType.KimiK2 => "https://kimi.moonshot.cn",
        ProviderType.VertexAI => "https://cloud.google.com/vertex-ai",
        ProviderType.Zai => "https://z.ai",
        ProviderType.Factory => "https://factory.ai",
        ProviderType.Antigravity => "https://antigravity.dev",
        ProviderType.OpenCode => "https://opencode.dev",
        ProviderType.MiniMax => "https://minimax.chat",
        _ => ""
    };

    /// <summary>
    /// Gets the color associated with the provider for UI
    /// </summary>
    public static string GetBrandColor(this ProviderType provider) => provider switch
    {
        ProviderType.Claude => "#D97757",
        ProviderType.Codex => "#10A37F",
        ProviderType.Cursor => "#7C3AED",
        ProviderType.Copilot => "#000000",
        ProviderType.Gemini => "#4285F4",
        ProviderType.Augment => "#FF6B35",
        ProviderType.Amp => "#5046E5",
        ProviderType.JetBrains => "#000000",
        ProviderType.Kiro => "#FF9500",
        ProviderType.Kimi => "#7B68EE",
        ProviderType.KimiK2 => "#7B68EE",
        ProviderType.VertexAI => "#4285F4",
        ProviderType.Zai => "#1DA1F2",
        ProviderType.Factory => "#FF4500",
        ProviderType.Antigravity => "#8B5CF6",
        ProviderType.OpenCode => "#059669",
        ProviderType.MiniMax => "#FF6600",
        _ => "#6B7280"
    };
}
