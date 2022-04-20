using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Penumbra              _penumbra;
    private readonly SettingsTab           _settingsTab;
    private readonly ModFileSystemSelector _selector;
    private readonly ModPanel              _modPanel;
    private readonly CollectionsTab        _collectionsTab;
    private readonly EffectiveTab          _effectiveTab;
    private readonly DebugTab              _debugTab;
    private readonly ResourceTab           _resourceTab;

    public ConfigWindow( Penumbra penumbra )
        : base( GetLabel() )
    {
        _penumbra       = penumbra;
        _settingsTab    = new SettingsTab( this );
        _selector       = new ModFileSystemSelector( _penumbra.ModFileSystem, new HashSet< Mod2 >() ); // TODO
        _modPanel       = new ModPanel( this );
        _collectionsTab = new CollectionsTab( this );
        _effectiveTab   = new EffectiveTab();
        _debugTab       = new DebugTab( this );
        _resourceTab    = new ResourceTab( this );

        Dalamud.PluginInterface.UiBuilder.DisableGposeUiHide    = true;
        Dalamud.PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
        Dalamud.PluginInterface.UiBuilder.DisableUserUiHide     = true;
        RespectCloseHotkey                                      = true;
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2( 800, 600 ),
            MaximumSize = new Vector2( 4096, 2160 ),
        };
    }

    public override void Draw()
    {
        using var bar = ImRaii.TabBar( string.Empty, ImGuiTabBarFlags.NoTooltip );
        SetupSizes();
        _settingsTab.Draw();
        DrawModsTab();
        _collectionsTab.Draw();
        DrawChangedItemTab();
        _effectiveTab.Draw();
        _debugTab.Draw();
        _resourceTab.Draw();
    }

    public void Dispose()
    {
        _selector.Dispose();
    }

    private static string GetLabel()
        => Penumbra.Version.Length == 0
            ? "Penumbra###PenumbraConfigWindow"
            : $"Penumbra v{Penumbra.Version}###PenumbraConfigWindow";

    private Vector2 _defaultSpace;
    private Vector2 _inputTextWidth;

    private void SetupSizes()
    {
        _defaultSpace   = new Vector2( 0, 10 * ImGuiHelpers.GlobalScale );
        _inputTextWidth = new Vector2( 350f  * ImGuiHelpers.GlobalScale, 0 );
    }
}