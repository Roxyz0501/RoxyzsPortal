using System.Net.Http;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Roxyz0501.Dalamud.CommonUi;

namespace RoxyzsPortal;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IClientState client;
    private readonly IFramework framework;
    private readonly ITextureProvider textures;
    private readonly ConfigurationStore settings;
    private readonly LocalizedFonts fonts;
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 1024 * 1024 };
    private readonly CancellationTokenSource stop = new();
    private readonly CancellationTokenRegistration shutdown, unloading;
    private readonly Catalog catalog;
    private readonly Dictionary<Entry, Task<string?>> icons = [];
    private Dictionary<string, Version> installed = [];
    private volatile bool installedChanged = true;
    private Task<CatalogResult>? pending;
    private Snapshot? snapshot;
    private bool open;
    private string status = "";
    private const string Command = "/roxyzsportal";

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IClientState client, IFramework framework,
        ITextureProvider textures, IGameLifecycle lifecycle, IPluginLog log)
    {
        this.pi = pi;
        this.commands = commands;
        this.client = client;
        this.framework = framework;
        this.textures = textures;
        catalog = new(Path.Combine(pi.ConfigDirectory.FullName, "cache"), http);
        settings = new(pi.ConfigFile.FullName, (key, ex) => log.Warning(ex, "{Status}", key));
        var code = Loc.Resolve(settings.Value.Language, client.ClientLanguage, pi.UiLanguage);
        if (settings.Value.Language != code) settings.Edit(c => c.Language = code);
        fonts = new(pi.UiBuilder.FontAtlas, KnownEntries.Items.Values.Select(x => x.Japanese));
        commands.AddHandler(Command, new CommandInfo((_, _) => Open()) { HelpMessage = T("Help") });
        pi.UiBuilder.OpenMainUi += Open;
        pi.UiBuilder.OpenConfigUi += Open;
        pi.UiBuilder.Draw += Draw;
        pi.ActivePluginsChanged += Changed;
        client.Logout += Logout;
        framework.Update += Update;
        shutdown = lifecycle.GameShuttingDownToken.Register(settings.Flush);
        unloading = lifecycle.DalamudUnloadingToken.Register(settings.Flush);
    }

    private string T(string key) => Loc.Get(settings.Value.Language, key);
    private void Open()
    {
        if (!open) Refresh(false);
        open = true;
        installedChanged = true;
    }

    private void Refresh(bool force)
    {
        if (pending != null) return;
        var previous = snapshot;
        pending = Task.Run(() => catalog.Load(previous, force, stop.Token));
        status = "Loading";
        if (force)
        {
            foreach (var pair in icons.Where(x => x.Value.IsCompletedSuccessfully && x.Value.Result == null).ToArray())
                icons.Remove(pair.Key);
        }
    }

    private void Changed(IActivePluginsChangedEventArgs _) => installedChanged = true;
    private void Logout(int _, int __) => settings.Flush();
    private void Update(IFramework _)
    {
        settings.Tick();
        if (pending is { IsCompleted: true })
        {
            try
            {
                var result = pending.GetAwaiter().GetResult();
                snapshot = result.Data;
                status = result.Offline ? "Offline" : "";
                var current = snapshot.Entries.ToHashSet();
                foreach (var key in icons.Where(x => x.Value.IsCompleted && !current.Contains(x.Key)).Select(x => x.Key).ToArray())
                    icons.Remove(key);
            }
            catch (Exception) { status = "FetchError"; }
            pending = null;
        }
        if (open && installedChanged)
        {
            installedChanged = false;
            installed = pi.InstalledPlugins.GroupBy(p => p.InternalName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Version)!, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Draw()
    {
        if (!open) return;
        using var font = fonts.Push(settings.Value.Language);
        ImGui.SetNextWindowSize(new Vector2(830, 620), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("RoxyzsPortal###RoxyzsPortal", ref open) && ImGui.BeginTabBar("tabs"))
        {
            if (ImGui.BeginTabItem(T("Plugins") + "###Catalog"))
            {
                DrawCatalog();
                ImGui.EndTabItem();
            }
            StandardPluginTabs.DrawStandardTrailingTabs(T("Settings"), "Settings", DrawSettings,
                T("Language"), DrawLanguage, T("Support"), () => StandardPluginTabs.DrawSupportContent(
                T("SupportHeading"), T("SupportBody"), T("Recipient"), T("SupportButton")));
            ImGui.EndTabBar();
        }
        ImGui.End();
    }

    private void DrawCatalog()
    {
        ImGui.BeginDisabled(pending != null);
        if (ImGui.Button(T("Refresh"))) Refresh(true);
        ImGui.EndDisabled();
        if (status.Length != 0) { ImGui.SameLine(); ImGui.TextWrapped(T(status)); }
        if (snapshot == null) return;
        ImGui.TextWrapped(T("Confirm"));
        if (snapshot.Entries.Length == 0) ImGui.TextUnformatted(T("Empty"));
        if (ImGui.BeginChild("entries"))
        {
            foreach (var row in snapshot.Entries)
            {
                ImGui.PushID(row.InternalName);
                ImGui.Separator();
                var size = new Vector2(ImGui.GetTextLineHeight() * 2.8f);
                var position = ImGui.GetCursorScreenPos();
                ImGui.Dummy(size);
                if (ImGui.IsItemVisible()) DrawIcon(row, position, size);
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.TextUnformatted(row.Name);
                installed.TryGetValue(row.InternalName, out var version);
                bool canUpdate = version != null && version < row.AssemblyVersion;
                var label = version == null ? "Download" : canUpdate ? "Upgrade" : "Installed";
                ImGui.BeginDisabled(version != null && !canUpdate);
                if (ImGui.SmallButton(T(label)))
                {
                    var page = canUpdate ? PluginInstallerOpenKind.UpdateablePlugins : PluginInstallerOpenKind.AllPlugins;
                    if (!pi.OpenPluginInstallerTo(page, row.Name)) status = "OperationFailed";
                }
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.TextDisabled(canUpdate ? $"{version} → {row.AssemblyVersion}" : row.AssemblyVersion.ToString());
                ImGui.EndGroup();
                KnownEntries.Items.TryGetValue(row.InternalName, out var known);
                var description = settings.Value.Language == "ja" && known.Version == row.AssemblyVersion.ToString()
                    ? known.Japanese : row.Description;
                ImGui.TextWrapped(string.IsNullOrWhiteSpace(description) ? row.Punchline ?? "—" : description);
                var created = row.CreatedAt ?? known.Created;
                ImGui.TextWrapped($"{T("Created")}  {Date(created)}      {T("Updated")}  {Date(row.LastUpdate)}");
                ImGui.Spacing();
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
    }

    private static string Date(long? seconds) => seconds is > 0 and <= 253402300799
        ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value).LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "—";

    private void DrawIcon(Entry row, Vector2 at, Vector2 size)
    {
        if (!icons.TryGetValue(row, out var task))
            icons[row] = task = Task.Run(() => catalog.Icon(row, stop.Token));
        var draw = ImGui.GetWindowDrawList();
        if (task.IsCompletedSuccessfully && task.Result is { } path &&
            textures.GetFromFile(path).TryGetWrap(out var image, out _))
            draw.AddImage(image.Handle, at, at + size);
        else
        {
            draw.AddRectFilled(at, at + size, 0xff554337, 5);
            draw.AddText(at + (size - ImGui.CalcTextSize("R")) / 2, 0xffe5d9ba, "R");
        }
    }

    private void DrawSettings()
    {
        ImGui.TextWrapped(T("Repo"));
        ImGui.TextWrapped(Catalog.Index);
        if (ImGui.Button(T("Copy"))) ImGui.SetClipboardText(Catalog.Index);
        if (settings.Notice.Length > 0)
        {
            ImGui.TextWrapped(T(settings.Notice));
            if (ImGui.Button(T("Retry"))) settings.Retry();
        }
    }

    private void DrawLanguage()
    {
        var code = StandardPluginTabs.DrawLanguageSelector(T("Language"), T("LangHelp"), settings.Value.Language ?? "en");
        if (code != null) settings.Edit(c => c.Language = code);
    }

    public void Dispose()
    {
        framework.Update -= Update;
        client.Logout -= Logout;
        pi.ActivePluginsChanged -= Changed;
        pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenMainUi -= Open;
        pi.UiBuilder.OpenConfigUi -= Open;
        commands.RemoveHandler(Command);
        shutdown.Dispose();
        unloading.Dispose();
        stop.Cancel();
        var tasks = icons.Values.Cast<Task>().ToList();
        if (pending != null) tasks.Add(pending);
        try { Task.WhenAll(tasks).GetAwaiter().GetResult(); } catch (Exception) { }
        http.Dispose();
        stop.Dispose();
        fonts.Dispose();
        settings.Dispose();
    }
}
