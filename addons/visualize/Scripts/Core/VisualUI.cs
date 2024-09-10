﻿using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using Visualize.Utils;
using static Godot.Control;

namespace Visualize;

public static class VisualUI
{
    public const float VISUAL_UI_SCALE_FACTOR = 0.6f;

    public static List<Action> CreateVisualPanels(List<VisualNode> debugVisualNodes, List<VisualSpinBox> debugExportSpinBoxes)
    {
        Dictionary<Node, VBoxContainer> visualNodes = new();
        List<Action> updateControls = new();

        foreach (VisualNode debugVisualNode in debugVisualNodes)
        {
            Node node = debugVisualNode.Node;

            VBoxContainer vboxMembers = CreateVisualContainer(node.Name);

            string[] visualizeMembers = debugVisualNode.VisualizeMembers;

            if (visualizeMembers != null)
            {
                foreach (string visualMember in visualizeMembers)
                {
                    PropertyInfo property = node.GetType().GetProperty(visualMember);

                    object initialValue = property.GetValue(node);
                    
                    (Control, List<Control>) element = VisualControlTypes.CreateControlForType(initialValue, property.PropertyType, debugExportSpinBoxes, v =>
                    {
                        // Do nothing
                    });

                    if (property.PropertyType == typeof(Vector2))
                    {

                    }

                    foreach (Control control in element.Item2)
                    {
                        Action action = () => { };

                        if (control is SpinBox spinBox)
                        {
                            spinBox.Editable = false;

                            action = () => spinBox.Value = (double)Convert.ChangeType(property.GetValue(node), typeof(double));

                        }
                        else if (control is LineEdit lineEdit)
                        {
                            lineEdit.Editable = false;
                        }
                        else if (control is BaseButton baseButton)
                        {
                            baseButton.Disabled = true;
                        }

                        updateControls.Add(action);
                    }

                    vboxMembers.AddChild(element.Item1);
                }
            }

            AddMemberInfoElements(vboxMembers, debugVisualNode.Properties, node, debugExportSpinBoxes);

            AddMemberInfoElements(vboxMembers, debugVisualNode.Fields, node, debugExportSpinBoxes);

            VisualMethods.AddMethodInfoElements(vboxMembers, debugVisualNode.Methods, node, debugExportSpinBoxes);

            VBoxContainer vboxLogs = new();
            vboxMembers.AddChild(vboxLogs);

            visualNodes.Add(node, vboxLogs);

            // Add vbox to scene tree to get vbox.Size for later
            node.AddChild(vboxMembers);

            // Using RigidBodies as a temporary workaround to overlapping visual panels
            // Of course updating the control positions would be better but I'm not sure
            // how to do this right now
            RigidBody2D rigidBody = CreateRigidBody(vboxMembers);

            // Reparent vbox to rigidbody
            vboxMembers.GetParent().RemoveChild(vboxMembers);
            rigidBody.AddChild(vboxMembers);
            node.AddChild(rigidBody);

            // All debug UI elements should not be influenced by the game world environments lighting
            node.GetChildren<Control>().ForEach(child => child.SetUnshaded());

            vboxMembers.Scale = Vector2.One * VISUAL_UI_SCALE_FACTOR;

            if (debugVisualNode.InitialPosition != Vector2.Zero)
            {
                vboxMembers.GlobalPosition = debugVisualNode.InitialPosition;
            }
        }

        // This is ugly but I don't know how else to do it
        VisualLogger.VisualNodes = visualNodes;

        return updateControls;
    }

    private static RigidBody2D CreateRigidBody(VBoxContainer vbox)
    {
        RigidBody2D rigidBody = new()
        {
            GravityScale = 0,
            LockRotation = true
        };
        rigidBody.SetCollisionLayerAndMask(32);

        CollisionShape2D collision = new()
        {
            Shape = new RectangleShape2D
            {
                Size = vbox.Size
            },
            Position = vbox.Size / 2
        };

        rigidBody.AddChild(collision);

        return rigidBody;
    }

    private static VBoxContainer CreateVisualContainer(string nodeName)
    {
        VBoxContainer vbox = new()
        {
            // Ensure this info is rendered above all game elements
            ZIndex = (int)RenderingServer.CanvasItemZMax
        };

        Label label = new() { Text = nodeName };

        vbox.AddChild(label);

        return vbox;
    }

    private static void AddMemberInfoElements(VBoxContainer vbox, IEnumerable<MemberInfo> members, Node node, List<VisualSpinBox> debugExportSpinBoxes)
    {
        foreach (MemberInfo member in members)
        {
            Control element = CreateMemberInfoElement(member, node, debugExportSpinBoxes);
            vbox.AddChild(element);
        }
    }

    private static HBoxContainer CreateMemberInfoElement(MemberInfo member, Node node, List<VisualSpinBox> debugExportSpinBoxes)
    {
        HBoxContainer hbox = new();

        Type type = VisualHandler.GetMemberType(member);

        object initialValue = VisualHandler.GetMemberValue(member, node);

        (Control, List<Control>) element = VisualControlTypes.CreateControlForType(initialValue, type, debugExportSpinBoxes, v =>
        {
            VisualHandler.SetMemberValue(member, node, v);
        });

        if (element.Item1 != null)
        {
            Label label = new()
            {
                Text = member.Name.ToPascalCase().AddSpaceBeforeEachCapital(),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            hbox.AddChild(label);
            hbox.AddChild(element.Item1);
        }

        return hbox;
    }
}
