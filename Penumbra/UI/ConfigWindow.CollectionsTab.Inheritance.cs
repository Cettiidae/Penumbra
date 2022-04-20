using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.UI.Classes;
using Penumbra.Util;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class CollectionsTab
    {
        private const int    InheritedCollectionHeight = 10;
        private const string InheritanceDragDropLabel  = "##InheritanceMove";

        // Keep for reuse.
        private readonly HashSet< ModCollection > _seenInheritedCollections = new(32);

        // Execute changes only outside of loops.
        private ModCollection? _newInheritance;
        private ModCollection? _movedInheritance;
        private (int, int)?    _inheritanceAction;
        private ModCollection? _newCurrentCollection;

        // Draw the whole inheritance block.
        private void DrawInheritanceBlock()
        {
            using var id = ImRaii.PushId( "##Inheritance" );
            DrawCurrentCollectionInheritance();
            DrawInheritanceTrashButton();
            DrawNewInheritanceSelection();
            DelayedActions();
        }

        // If an inherited collection is expanded,
        // draw all its flattened, distinct children in order with a tree-line.
        private void DrawInheritedChildren( ModCollection collection )
        {
            using var id     = ImRaii.PushId( collection.Index );
            using var indent = ImRaii.PushIndent();

            // Get start point for the lines (top of the selector).
            // Tree line stuff.
            var lineStart = ImGui.GetCursorScreenPos();
            var offsetX   = -ImGui.GetStyle().IndentSpacing + ImGui.GetTreeNodeToLabelSpacing() / 2;
            var drawList  = ImGui.GetWindowDrawList();
            var lineSize  = Math.Max( 0, ImGui.GetStyle().IndentSpacing - 9 * ImGuiHelpers.GlobalScale );
            lineStart.X += offsetX;
            lineStart.Y -= 2 * ImGuiHelpers.GlobalScale;
            var lineEnd = lineStart;

            // Skip the collection itself.
            foreach( var inheritance in collection.GetFlattenedInheritance().Skip( 1 ) )
            {
                // Draw the child, already seen collections are colored as conflicts.
                using var color = ImRaii.PushColor( ImGuiCol.Text, ColorId.HandledConflictMod.Value(),
                    _seenInheritedCollections.Contains( inheritance ) );
                _seenInheritedCollections.Add( inheritance );

                ImRaii.TreeNode( inheritance.Name, ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet );
                var (minRect, maxRect) = ( ImGui.GetItemRectMin(), ImGui.GetItemRectMax() );
                DrawInheritanceTreeClicks( inheritance, false );

                // Tree line stuff.
                if( minRect.X == 0 )
                {
                    continue;
                }

                // Draw the notch and increase the line length.
                var midPoint = ( minRect.Y + maxRect.Y ) / 2f - 1f;
                drawList.AddLine( new Vector2( lineStart.X, midPoint ), new Vector2( lineStart.X + lineSize, midPoint ), Colors.MetaInfoText,
                    ImGuiHelpers.GlobalScale );
                lineEnd.Y = midPoint;
            }

            // Finally, draw the folder line.
            drawList.AddLine( lineStart, lineEnd, Colors.MetaInfoText, ImGuiHelpers.GlobalScale );
        }

        // Draw a single primary inherited collection.
        private void DrawInheritance( ModCollection collection )
        {
            using var color = ImRaii.PushColor( ImGuiCol.Text, ColorId.HandledConflictMod.Value(),
                _seenInheritedCollections.Contains( collection ) );
            _seenInheritedCollections.Add( collection );
            using var tree = ImRaii.TreeNode( collection.Name, ImGuiTreeNodeFlags.NoTreePushOnOpen );
            color.Pop();
            DrawInheritanceTreeClicks( collection, true );
            DrawInheritanceDropSource( collection );
            DrawInheritanceDropTarget( collection );

            if( tree )
            {
                DrawInheritedChildren( collection );
            }
            else
            {
                // We still want to keep track of conflicts.
                _seenInheritedCollections.UnionWith( collection.GetFlattenedInheritance() );
            }
        }

        // Draw the list box containing the current inheritance information.
        private void DrawCurrentCollectionInheritance()
        {
            using var list = ImRaii.ListBox( "##inheritanceList",
                new Vector2( _window._inputTextWidth.X - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X,
                    ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight ) );
            if( !list )
            {
                return;
            }

            _seenInheritedCollections.Clear();
            _seenInheritedCollections.Add( Penumbra.CollectionManager.Current );
            foreach( var collection in Penumbra.CollectionManager.Current.Inheritance.ToList() )
            {
                DrawInheritance( collection );
            }
        }

        // Draw a drag and drop button to delete.
        private void DrawInheritanceTrashButton()
        {
            ImGui.SameLine();
            var size        = new Vector2( ImGui.GetFrameHeight(), ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight );
            var buttonColor = ImGui.GetColorU32( ImGuiCol.Button );
            // Prevent hovering from highlighting the button.
            using var color = ImRaii.PushColor( ImGuiCol.ButtonActive, buttonColor )
               .Push( ImGuiCol.ButtonHovered, buttonColor );
            ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), size,
                "Drag primary inheritance here to remove it from the list.", false, true );

            using var target = ImRaii.DragDropTarget();
            if( target.Success && ImGuiUtil.IsDropping( InheritanceDragDropLabel ) )
            {
                _inheritanceAction = ( Penumbra.CollectionManager.Current.Inheritance.IndexOf( _movedInheritance ), -1 );
            }
        }

        // Set the current collection, or delete or move an inheritance if the action was triggered during iteration.
        // Can not be done during iteration to keep collections unchanged.
        private void DelayedActions()
        {
            if( _newCurrentCollection != null )
            {
                Penumbra.CollectionManager.SetCollection( _newCurrentCollection, ModCollection.Type.Current );
                _newCurrentCollection = null;
            }

            if( _inheritanceAction == null )
            {
                return;
            }

            if( _inheritanceAction.Value.Item1 >= 0 )
            {
                if( _inheritanceAction.Value.Item2 == -1 )
                {
                    Penumbra.CollectionManager.Current.RemoveInheritance( _inheritanceAction.Value.Item1 );
                }
                else
                {
                    Penumbra.CollectionManager.Current.MoveInheritance( _inheritanceAction.Value.Item1, _inheritanceAction.Value.Item2 );
                }
            }

            _inheritanceAction = null;
        }

        // Draw the selector to add new inheritances.
        // The add button is only available if the selected collection can actually be added.
        private void DrawNewInheritanceSelection()
        {
            DrawNewInheritanceCombo();
            ImGui.SameLine();
            var inheritance = Penumbra.CollectionManager.Current.CheckValidInheritance( _newInheritance );
            var tt = inheritance switch
            {
                ModCollection.ValidInheritance.Empty     => "No valid collection to inherit from selected.",
                ModCollection.ValidInheritance.Valid     => "Add a new inheritance to the collection.",
                ModCollection.ValidInheritance.Self      => "Can not inherit from itself.",
                ModCollection.ValidInheritance.Contained => "Already inheriting from the selected collection.",
                ModCollection.ValidInheritance.Circle    => "Inheriting from selected collection would lead to cyclic inheritance.",
                _                                        => string.Empty,
            };
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), ImGui.GetFrameHeight() * Vector2.One, tt,
                   inheritance != ModCollection.ValidInheritance.Valid, true )
            && Penumbra.CollectionManager.Current.AddInheritance( _newInheritance! ) )
            {
                _newInheritance = null;
            }

            if( inheritance != ModCollection.ValidInheritance.Valid )
            {
                _newInheritance = null;
            }

            ImGuiComponents.HelpMarker( tt );
        }

        // Draw the combo to select new potential inheritances.
        // Only valid inheritances are drawn in the preview, or nothing if no inheritance is available.
        private void DrawNewInheritanceCombo()
        {
            ImGui.SetNextItemWidth( _window._inputTextWidth.X - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X );
            _newInheritance ??= Penumbra.CollectionManager.FirstOrDefault( c
                    => c != Penumbra.CollectionManager.Current && !Penumbra.CollectionManager.Current.Inheritance.Contains( c ) )
             ?? ModCollection.Empty;
            using var combo = ImRaii.Combo( "##newInheritance", _newInheritance.Name );
            if( !combo )
            {
                return;
            }

            foreach( var collection in Penumbra.CollectionManager
                       .Where( c => Penumbra.CollectionManager.Current.CheckValidInheritance( c ) == ModCollection.ValidInheritance.Valid ) )
            {
                if( ImGui.Selectable( collection.Name, _newInheritance == collection ) )
                {
                    _newInheritance = collection;
                }
            }
        }

        // Move an inherited collection when dropped onto another.
        // Move is delayed due to collection changes.
        private void DrawInheritanceDropTarget( ModCollection collection )
        {
            using var target = ImRaii.DragDropTarget();
            if( target.Success && ImGuiUtil.IsDropping( InheritanceDragDropLabel ) )
            {
                if( _movedInheritance != null )
                {
                    var idx1 = Penumbra.CollectionManager.Current.Inheritance.IndexOf( _movedInheritance );
                    var idx2 = Penumbra.CollectionManager.Current.Inheritance.IndexOf( collection );
                    if( idx1 >= 0 && idx2 >= 0 )
                    {
                        _inheritanceAction = ( idx1, idx2 );
                    }
                }

                _movedInheritance = null;
            }
        }

        // Move an inherited collection.
        private void DrawInheritanceDropSource( ModCollection collection )
        {
            using var source = ImRaii.DragDropSource();
            if( source )
            {
                ImGui.SetDragDropPayload( InheritanceDragDropLabel, IntPtr.Zero, 0 );
                _movedInheritance = collection;
                ImGui.Text( $"Moving {_movedInheritance?.Name ?? "Unknown"}..." );
            }
        }

        // Ctrl + Right-Click -> Switch current collection to this (for all).
        // Ctrl + Shift + Right-Click -> Delete this inheritance (only if withDelete).
        // Deletion is delayed due to collection changes.
        private void DrawInheritanceTreeClicks( ModCollection collection, bool withDelete )
        {
            if( ImGui.GetIO().KeyCtrl && ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
            {
                if( withDelete && ImGui.GetIO().KeyShift )
                {
                    _inheritanceAction = ( Penumbra.CollectionManager.Current.Inheritance.IndexOf( collection ), -1 );
                }
                else
                {
                    _newCurrentCollection = collection;
                }
            }

            ImGuiUtil.HoverTooltip( "Control + Right-Click to switch the current collection to this one."
              + ( withDelete ? "\nControl + Shift + Right-Click to remove this inheritance." : string.Empty ) );
        }
    }
}